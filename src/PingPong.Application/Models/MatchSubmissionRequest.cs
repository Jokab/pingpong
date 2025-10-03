namespace PingPong.Application.Models;

public sealed record MatchSubmissionRequest(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly MatchDate,
    IReadOnlyList<SetScore> Sets,
    string? SubmittedBy);
