using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.Entities;
using PingPong.Domain.Exceptions;
using PingPong.Domain.ValueObjects;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class MatchSubmissionService : IMatchSubmissionService
{
    private readonly PingPongDbContext _context;
    private readonly IPlayerDirectory _playerDirectory;
    private readonly IRatingService _ratingService;

    public MatchSubmissionService(PingPongDbContext context, IPlayerDirectory playerDirectory, IRatingService ratingService)
    {
        _context = context;
        _playerDirectory = playerDirectory;
        _ratingService = ratingService;
    }

    public async Task<MatchSubmissionResult> SubmitMatchAsync(MatchSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hasSets = request.Sets is not null && request.Sets.Count > 0;
        if (!hasSets && request.PlayerOneWon is null)
        {
            throw new DomainValidationException("At least sets or PlayerOneWon must be provided.");
        }

        var playerOne = await _playerDirectory.EnsurePlayerAsync(request.PlayerOneName, cancellationToken);
        var playerTwo = await _playerDirectory.EnsurePlayerAsync(request.PlayerTwoName, cancellationToken);

        var submittedAt = DateTimeOffset.UtcNow;

        if (hasSets)
        {
            var setScores = (request.Sets ?? Array.Empty<SetScore>())
                .OrderBy(set => set.SetNumber)
                .Select(set => new MatchSetScore(set.PlayerOneScore, set.PlayerTwoScore))
                .ToList();

            // Validate sets with existing domain rules
            for (var i = 0; i < setScores.Count; i++)
            {
                var score = setScores[i];
                if (score.PlayerOneScore < 0 || score.PlayerTwoScore < 0)
                    throw new DomainValidationException($"Set {i + 1} scores must be non-negative.");
                var maxScore = Math.Max(score.PlayerOneScore, score.PlayerTwoScore);
                var minScore = Math.Min(score.PlayerOneScore, score.PlayerTwoScore);
                if (maxScore < 11)
                    throw new DomainValidationException($"Set {i + 1} winning score must be at least 11.");
                if (maxScore - minScore < 2)
                    throw new DomainValidationException($"Set {i + 1} must be won by at least two points.");
                if (score.PlayerOneScore == score.PlayerTwoScore)
                    throw new DomainValidationException($"Set {i + 1} cannot end in a draw.");
            }

            var playerOneSetsWon = setScores.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
            var playerTwoSetsWon = setScores.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
            if (playerOneSetsWon == playerTwoSetsWon)
            {
                throw new DomainValidationException("Match submission must produce a clear winner.");
            }

            var matchEventId = Guid.NewGuid();
            var matchEvent = new ScoredMatchEvent
            {
                Id = matchEventId,
                EventType = MatchEventType.Recorded,
                PlayerOneId = playerOne.Id,
                PlayerTwoId = playerTwo.Id,
                MatchDate = request.MatchDate,
                CreatedAt = submittedAt,
                SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy) ? null : request.SubmittedBy!.Trim(),
                PlayerOne = playerOne,
                PlayerTwo = playerTwo,
                Sets = setScores.Select((s, idx) => new MatchEventSet
                {
                    Id = Guid.NewGuid(),
                    MatchEventId = matchEventId,
                    SetNumber = idx + 1,
                    PlayerOneScore = s.PlayerOneScore,
                    PlayerTwoScore = s.PlayerTwoScore
                }).ToList()
            };

            // Ensure EF doesn't enforce FK to a Match row: keep default
            matchEvent.MatchId = matchEvent.MatchId;
            _context.MatchEvents.Add(matchEvent);
            await _context.SaveChangesAsync(cancellationToken);

            await _ratingService.RebuildAllRatingsAsync(cancellationToken);
            return new MatchSubmissionResult(Guid.Empty, matchEvent.Id);
        }
        else
        {
            var matchEvent = new OutcomeOnlyMatchEvent
            {
                Id = Guid.NewGuid(),
                EventType = MatchEventType.Recorded,
                PlayerOneId = playerOne.Id,
                PlayerTwoId = playerTwo.Id,
                MatchDate = request.MatchDate,
                CreatedAt = submittedAt,
                SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy) ? null : request.SubmittedBy!.Trim(),
                PlayerOne = playerOne,
                PlayerTwo = playerTwo,
                PlayerOneWon = request.PlayerOneWon!.Value
            };

            _context.MatchEvents.Add(matchEvent);
            await _context.SaveChangesAsync(cancellationToken);

            await _ratingService.RebuildAllRatingsAsync(cancellationToken);
            return new MatchSubmissionResult(Guid.Empty, matchEvent.Id);
        }
    }
}
