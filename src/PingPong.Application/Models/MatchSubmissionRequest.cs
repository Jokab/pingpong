namespace PingPong.Application.Models;

public sealed record MatchSubmissionRequest(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly MatchDate,
    IReadOnlyList<SetScore> Sets,
    IReadOnlyList<SetWinner> OutcomeOnlySets,
    bool? PlayerOneWon,
    string? SubmittedBy,
    Guid? TournamentFixtureId);

public sealed record SetScore(int SetNumber, int PlayerOneScore, int PlayerTwoScore);

public sealed record SetWinner(int SetNumber, bool PlayerOneWon);
