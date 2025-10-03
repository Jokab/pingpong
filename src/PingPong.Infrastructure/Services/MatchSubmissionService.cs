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

    public MatchSubmissionService(PingPongDbContext context, IPlayerDirectory playerDirectory)
    {
        _context = context;
        _playerDirectory = playerDirectory;
    }

    public async Task<MatchSubmissionResult> SubmitMatchAsync(MatchSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Sets is null || request.Sets.Count == 0)
        {
            throw new DomainValidationException("At least one set score must be provided.");
        }

        var playerOne = await _playerDirectory.EnsurePlayerAsync(request.PlayerOneName, cancellationToken);
        var playerTwo = await _playerDirectory.EnsurePlayerAsync(request.PlayerTwoName, cancellationToken);

        var setScores = request.Sets
            .OrderBy(set => set.SetNumber)
            .Select(set => new MatchSetScore(set.PlayerOneScore, set.PlayerTwoScore))
            .ToList();

        var submittedAt = DateTimeOffset.UtcNow;

        // Validate sets with existing domain rules
        for (var i = 0; i < setScores.Count; i++)
        {
            // Use MatchAggregate's internal validation by creating a transient aggregate, but we won't persist Match
            // Alternatively, re-apply the same validation locally to avoid constructing the Match entity.
            // Here, we perform local validation to keep persistence event-sourced.
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
        var matchEvent = new MatchEvent
        {
            Id = matchEventId,
            // Leave MatchId unset (default Guid) and do not set Match navigation
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

        // Ensure EF doesn't enforce FK to a Match row: explicit null for navigation and default MatchId
        matchEvent.MatchId = matchEvent.MatchId; // keep as default
        await _context.MatchEvents.AddAsync(matchEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new MatchSubmissionResult(Guid.Empty, matchEvent.Id);
    }
}
