namespace PingPong.Application.Models;

public sealed record MatchSubmissionRequest(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly MatchDate,
    IReadOnlyList<SetScore> Sets,
    string? SubmittedBy);

public sealed record SetScore(int SetNumber, int PlayerOneScore, int PlayerTwoScore);
