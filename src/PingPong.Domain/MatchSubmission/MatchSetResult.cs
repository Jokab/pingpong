using PingPong.Domain.Exceptions;
namespace PingPong.Domain.MatchSubmission;

/// <summary>
/// Represents the outcome of a single set.
/// </summary>
public abstract record MatchSetResult
{
    protected MatchSetResult(int setNumber)
    {
        if (setNumber <= 0)
        {
            throw new DomainValidationException("SetNumber must be positive.");
        }

        SetNumber = setNumber;
    }

    public int SetNumber { get; }

    public abstract bool PlayerOneWon { get; }
}

public sealed record ScoredMatchSetResult(int SetNumber, MatchSetScore Score)
    : MatchSetResult(SetNumber)
{
    public override bool PlayerOneWon => Score.PlayerOneScore > Score.PlayerTwoScore;
}

public sealed record OutcomeOnlyMatchSetResult(int SetNumber, bool PlayerOneWonResult)
    : MatchSetResult(SetNumber)
{
    public override bool PlayerOneWon => PlayerOneWonResult;
}
