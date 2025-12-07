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
    private readonly ITournamentCommandService _tournamentCommandService;

    public MatchSubmissionService(
        PingPongDbContext context,
        IPlayerDirectory playerDirectory,
        IRatingService ratingService,
        ITournamentCommandService tournamentCommandService
    )
    {
        _context = context;
        _playerDirectory = playerDirectory;
        _ratingService = ratingService;
        _tournamentCommandService = tournamentCommandService;
    }

    public async Task<MatchSubmissionResult> SubmitMatchAsync(
        MatchSubmissionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoredSets = request.Sets;
        var outcomeOnlySets = request.OutcomeOnlySets;

        if (scoredSets.Count > 0 && outcomeOnlySets.Count > 0)
        {
            throw new DomainValidationException(
                "Cannot mix scored sets with outcome-only set winners."
            );
        }

        var hasScoredSets = scoredSets.Count > 0;
        var hasOutcomeOnlySets = outcomeOnlySets.Count > 0;

        if (!hasScoredSets && !hasOutcomeOnlySets && request.PlayerOneWon is null)
        {
            throw new DomainValidationException(
                "At least sets, outcome-only set winners, or PlayerOneWon must be provided."
            );
        }

        var playerOne = await _playerDirectory.EnsurePlayerAsync(
            request.PlayerOneName,
            cancellationToken
        );
        var playerTwo = await _playerDirectory.EnsurePlayerAsync(
            request.PlayerTwoName,
            cancellationToken
        );

        var submittedAt = DateTimeOffset.UtcNow;

        MatchEvent matchEvent;
        Guid winnerPlayerId;

        if (hasScoredSets)
        {
            var setScores = scoredSets
                .OrderBy(set => set.SetNumber)
                .Select(set => new MatchSetScore(set.PlayerOneScore, set.PlayerTwoScore))
                .ToList();

            // Validate sets with existing domain rules
            for (var i = 0; i < setScores.Count; i++)
            {
                var score = setScores[i];
                if (score.PlayerOneScore < 0 || score.PlayerTwoScore < 0)
                    throw new DomainValidationException(
                        $"Set {i + 1} scores must be non-negative."
                    );
                var maxScore = Math.Max(score.PlayerOneScore, score.PlayerTwoScore);
                var minScore = Math.Min(score.PlayerOneScore, score.PlayerTwoScore);
                if (maxScore < 11)
                    throw new DomainValidationException(
                        $"Set {i + 1} winning score must be at least 11."
                    );
                if (maxScore - minScore < 2)
                    throw new DomainValidationException(
                        $"Set {i + 1} must be won by at least two points."
                    );
                if (score.PlayerOneScore == score.PlayerTwoScore)
                    throw new DomainValidationException($"Set {i + 1} cannot end in a draw.");
            }

            var playerOneSetsWon = setScores.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
            var playerTwoSetsWon = setScores.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
            if (playerOneSetsWon == playerTwoSetsWon)
            {
                throw new DomainValidationException(
                    "Match submission must produce a clear winner."
                );
            }

            var matchEventId = Guid.NewGuid();
            matchEvent = new ScoredMatchEvent
            {
                Id = matchEventId,
                EventType = MatchEventType.Recorded,
                PlayerOneId = playerOne.Id,
                PlayerTwoId = playerTwo.Id,
                MatchDate = request.MatchDate,
                CreatedAt = submittedAt,
                SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy)
                    ? null
                    : request.SubmittedBy!.Trim(),
                PlayerOne = playerOne,
                PlayerTwo = playerTwo,
                Sets = setScores
                    .Select((s, idx) => MatchEventSetEntity.CreateScored(matchEventId, idx + 1, s))
                    .ToList(),
            };

            // Ensure EF doesn't enforce FK to a Match row: keep default
            matchEvent.MatchId = matchEvent.MatchId;
            winnerPlayerId = playerOneSetsWon > playerTwoSetsWon ? playerOne.Id : playerTwo.Id;
        }
        else if (hasOutcomeOnlySets)
        {
            var ordered = outcomeOnlySets.OrderBy(s => s.SetNumber).ToList();
            if (ordered.Any(s => s.SetNumber <= 0))
            {
                throw new DomainValidationException("Set numbers must be positive.");
            }

            if (ordered.GroupBy(s => s.SetNumber).Any(g => g.Count() > 1))
            {
                throw new DomainValidationException("Duplicate set numbers are not allowed.");
            }

            var p1SetsWon = ordered.Count(s => s.PlayerOneWon);
            var p2SetsWon = ordered.Count - p1SetsWon;
            if (p1SetsWon == p2SetsWon)
            {
                throw new DomainValidationException(
                    "Outcome-only sets must produce a clear winner."
                );
            }

            var derivedWinner = p1SetsWon > p2SetsWon;
            if (request.PlayerOneWon is not null && request.PlayerOneWon.Value != derivedWinner)
            {
                throw new DomainValidationException(
                    "PlayerOneWon does not match the provided set winners."
                );
            }

            var matchEventId = Guid.NewGuid();
            matchEvent = new OutcomeOnlyMatchEvent
            {
                Id = matchEventId,
                EventType = MatchEventType.Recorded,
                PlayerOneId = playerOne.Id,
                PlayerTwoId = playerTwo.Id,
                MatchDate = request.MatchDate,
                CreatedAt = submittedAt,
                SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy)
                    ? null
                    : request.SubmittedBy!.Trim(),
                PlayerOne = playerOne,
                PlayerTwo = playerTwo,
                PlayerOneWon = derivedWinner,
                Sets = ordered
                    .Select(set =>
                        MatchEventSetEntity.CreateOutcomeOnly(
                            matchEventId,
                            set.SetNumber,
                            set.PlayerOneWon
                        )
                    )
                    .ToList(),
            };
            winnerPlayerId = derivedWinner ? playerOne.Id : playerTwo.Id;
        }
        else
        {
            matchEvent = new OutcomeOnlyMatchEvent
            {
                Id = Guid.NewGuid(),
                EventType = MatchEventType.Recorded,
                PlayerOneId = playerOne.Id,
                PlayerTwoId = playerTwo.Id,
                MatchDate = request.MatchDate,
                CreatedAt = submittedAt,
                SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy)
                    ? null
                    : request.SubmittedBy!.Trim(),
                PlayerOne = playerOne,
                PlayerTwo = playerTwo,
                PlayerOneWon = request.PlayerOneWon!.Value,
            };

            winnerPlayerId = request.PlayerOneWon!.Value ? playerOne.Id : playerTwo.Id;
        }

        _context.MatchEvents.Add(matchEvent);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.TournamentFixtureId is Guid fixtureId)
        {
            await _tournamentCommandService.RecordFixtureResultAsync(
                fixtureId,
                winnerPlayerId,
                matchEvent.Id,
                cancellationToken
            );
        }

        await _ratingService.RebuildAllRatingsAsync(cancellationToken);
        return new MatchSubmissionResult(Guid.Empty, matchEvent.Id);
    }
}
