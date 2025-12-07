using PingPong.Domain.Exceptions;
namespace PingPong.Domain.MatchSubmission;

public sealed class MatchEventSetEntity
{
    public Guid Id { get; init; }

    public Guid MatchEventId { get; set; }

    public MatchEvent? MatchEvent { get; init; }

    public int SetNumber { get; init; }

    public int? PlayerOneScore { get; init; }

    public int? PlayerTwoScore { get; init; }

    public bool? PlayerOneWon { get; init; }

    public MatchSetResult ToSetResult() =>
        PlayerOneScore.HasValue && PlayerTwoScore.HasValue
            ? new ScoredMatchSetResult(
                SetNumber,
                new MatchSetScore(PlayerOneScore.Value, PlayerTwoScore.Value)
            )
        : PlayerOneWon.HasValue ? new OutcomeOnlyMatchSetResult(SetNumber, PlayerOneWon.Value)
        : throw new DomainValidationException(
            "MatchEventSetEntity must have either scores or a winner."
        );

    public static MatchEventSetEntity CreateScored(
        Guid matchEventId,
        int setNumber,
        MatchSetScore score
    )
    {
        ArgumentNullException.ThrowIfNull(score);

        return new MatchEventSetEntity
        {
            Id = Guid.NewGuid(),
            MatchEventId = matchEventId,
            SetNumber = setNumber,
            PlayerOneScore = score.PlayerOneScore,
            PlayerTwoScore = score.PlayerTwoScore,
            PlayerOneWon = null,
        };
    }

    public static MatchEventSetEntity CreateOutcomeOnly(
        Guid matchEventId,
        int setNumber,
        bool playerOneWon
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            MatchEventId = matchEventId,
            SetNumber = setNumber,
            PlayerOneScore = null,
            PlayerTwoScore = null,
            PlayerOneWon = playerOneWon,
        };
}
