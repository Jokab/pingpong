using PingPong.Domain.Entities;
using PingPong.Domain.Exceptions;
using PingPong.Domain.ValueObjects;

namespace PingPong.Domain.Aggregates;

public sealed class MatchAggregate
{
    private MatchAggregate(Match match, MatchEvent matchEvent)
    {
        Match = match;
        Event = matchEvent;
    }

    public Match Match { get; }

    public MatchEvent Event { get; }

    public static MatchAggregate CreateNew(
        Guid playerOneId,
        Guid playerTwoId,
        DateOnly matchDate,
        IReadOnlyList<MatchSetScore> sets,
        DateTimeOffset submittedAt,
        string? submittedBy)
    {
        if (playerOneId == Guid.Empty)
        {
            throw new DomainValidationException("Player one identifier is required.");
        }

        if (playerTwoId == Guid.Empty)
        {
            throw new DomainValidationException("Player two identifier is required.");
        }

        if (playerOneId == playerTwoId)
        {
            throw new DomainValidationException("A match requires two distinct players.");
        }

        ArgumentNullException.ThrowIfNull(sets);
        if (sets.Count == 0)
        {
            throw new DomainValidationException("At least one set score must be provided.");
        }

        var matchId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var match = new Match
        {
            Id = matchId,
            PlayerOneId = playerOneId,
            PlayerTwoId = playerTwoId,
            MatchDate = matchDate,
            Status = MatchStatus.Active,
            CreatedAt = submittedAt,
            UpdatedAt = submittedAt
        };

        var matchEvent = new MatchEvent
        {
            Id = eventId,
            MatchId = matchId,
            EventType = MatchEventType.Recorded,
            PlayerOneId = playerOneId,
            PlayerTwoId = playerTwoId,
            MatchDate = matchDate,
            CreatedAt = submittedAt,
            SubmittedBy = string.IsNullOrWhiteSpace(submittedBy) ? null : submittedBy.Trim()
        };

        var matchSets = new List<MatchSet>(sets.Count);
        var eventSets = new List<MatchEventSet>(sets.Count);

        var playerOneSetsWon = 0;
        var playerTwoSetsWon = 0;

        for (var index = 0; index < sets.Count; index++)
        {
            var set = sets[index];
            ValidateSetScore(set, index);

            var playerOneScore = set.PlayerOneScore;
            var playerTwoScore = set.PlayerTwoScore;

            if (playerOneScore == playerTwoScore)
            {
                throw new DomainValidationException($"Set {index + 1} cannot end in a draw.");
            }

            var winner = playerOneScore > playerTwoScore ? MatchWinner.PlayerOne : MatchWinner.PlayerTwo;
            if (winner == MatchWinner.PlayerOne)
            {
                playerOneSetsWon++;
            }
            else
            {
                playerTwoSetsWon++;
            }

            var setNumber = index + 1;
            var matchSetId = Guid.NewGuid();
            var eventSetId = Guid.NewGuid();

            var matchSet = new MatchSet
            {
                Id = matchSetId,
                MatchId = matchId,
                SetNumber = setNumber,
                PlayerOneScore = playerOneScore,
                PlayerTwoScore = playerTwoScore
            };

            var matchEventSet = new MatchEventSet
            {
                Id = eventSetId,
                MatchEventId = eventId,
                SetNumber = setNumber,
                PlayerOneScore = playerOneScore,
                PlayerTwoScore = playerTwoScore
            };

            matchSets.Add(matchSet);
            eventSets.Add(matchEventSet);
        }

        if (playerOneSetsWon == playerTwoSetsWon)
        {
            throw new DomainValidationException("Match submission must produce a clear winner.");
        }

        match.PlayerOneSetsWon = playerOneSetsWon;
        match.PlayerTwoSetsWon = playerTwoSetsWon;
        match.Sets = matchSets;

        matchEvent.Sets = eventSets;

        return new MatchAggregate(match, matchEvent);
    }

    private static void ValidateSetScore(MatchSetScore score, int index)
    {
        if (score.PlayerOneScore < 0 || score.PlayerTwoScore < 0)
        {
            throw new DomainValidationException($"Set {index + 1} scores must be non-negative.");
        }

        var maxScore = Math.Max(score.PlayerOneScore, score.PlayerTwoScore);
        var minScore = Math.Min(score.PlayerOneScore, score.PlayerTwoScore);

        if (maxScore < 11)
        {
            throw new DomainValidationException($"Set {index + 1} winning score must be at least 11.");
        }

        if (maxScore - minScore < 2)
        {
            throw new DomainValidationException($"Set {index + 1} must be won by at least two points.");
        }
    }

    private enum MatchWinner
    {
        PlayerOne,
        PlayerTwo
    }
}
