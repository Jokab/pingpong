using PingPong.Domain.Exceptions;
using PingPong.Domain.ValueObjects;

namespace PingPong.Domain.Entities;

public sealed class MatchEventSetEntity
{
    public Guid Id { get; set; }

    public Guid MatchEventId { get; set; }

    public MatchEvent? MatchEvent { get; set; }

    public int SetNumber { get; set; }

    public int? PlayerOneScore { get; set; }

    public int? PlayerTwoScore { get; set; }

    public bool? PlayerOneWon { get; set; }

    public MatchSetResult ToSetResult()
    {
        if (PlayerOneScore.HasValue && PlayerTwoScore.HasValue)
        {
            return new ScoredMatchSetResult(SetNumber, new MatchSetScore(PlayerOneScore.Value, PlayerTwoScore.Value));
        }

        if (PlayerOneWon.HasValue)
        {
            return new OutcomeOnlyMatchSetResult(SetNumber, PlayerOneWon.Value);
        }

        throw new DomainValidationException("MatchEventSetEntity must have either scores or a winner.");
    }

    public static MatchEventSetEntity CreateScored(Guid matchEventId, int setNumber, MatchSetScore score)
    {
        ArgumentNullException.ThrowIfNull(score);

        return new MatchEventSetEntity
        {
            Id = Guid.NewGuid(),
            MatchEventId = matchEventId,
            SetNumber = setNumber,
            PlayerOneScore = score.PlayerOneScore,
            PlayerTwoScore = score.PlayerTwoScore,
            PlayerOneWon = null
        };
    }

    public static MatchEventSetEntity CreateOutcomeOnly(Guid matchEventId, int setNumber, bool playerOneWon)
    {
        return new MatchEventSetEntity
        {
            Id = Guid.NewGuid(),
            MatchEventId = matchEventId,
            SetNumber = setNumber,
            PlayerOneScore = null,
            PlayerTwoScore = null,
            PlayerOneWon = playerOneWon
        };
    }
}
