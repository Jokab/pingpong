using PingPong.Domain.Exceptions;
namespace PingPong.Domain.MatchSubmission;

/// <summary>
/// Represents a single effective match outcome after applying event-log rules.
/// </summary>
public sealed record MatchOutcome(
    Guid EventId,
    Guid PlayerOneId,
    Guid PlayerTwoId,
    DateOnly MatchDate,
    bool PlayerOneWon,
    IReadOnlyList<MatchSetResult> Sets,
    string? SubmittedBy,
    DateTimeOffset CreatedAt
)
{
    public Guid WinnerId => PlayerOneWon ? PlayerOneId : PlayerTwoId;

    public static MatchOutcome Create(
        Guid eventId,
        Guid playerOneId,
        Guid playerTwoId,
        DateOnly matchDate,
        bool? playerOneWon,
        IReadOnlyList<MatchSetResult> sets,
        string? submittedBy,
        DateTimeOffset createdAt
    )
    {
        ArgumentNullException.ThrowIfNull(sets);

        if (sets.Count == 0)
        {
            throw new DomainValidationException("MatchOutcome requires at least one set.");
        }

        var derivedWinner = playerOneWon ?? DeriveMatchWinner(sets);

        return new MatchOutcome(
            eventId,
            playerOneId,
            playerTwoId,
            matchDate,
            derivedWinner,
            sets,
            string.IsNullOrWhiteSpace(submittedBy) ? null : submittedBy,
            createdAt
        );
    }

    private static bool DeriveMatchWinner(IReadOnlyList<MatchSetResult> sets)
    {
        var p1Sets = sets.Count(s => s.PlayerOneWon);
        var p2Sets = sets.Count - p1Sets;

        if (p1Sets == p2Sets)
        {
            throw new DomainValidationException("MatchOutcome sets must produce a clear winner.");
        }

        return p1Sets > p2Sets;
    }
}
