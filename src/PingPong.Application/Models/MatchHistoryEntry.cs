namespace PingPong.Application.Models;

public sealed record MatchHistoryEntry(
    Guid EventId,
    Guid MatchId,
    DateOnly MatchDate,
    int DaySequence,
    string PlayerOneName,
    string PlayerTwoName,
    IReadOnlyList<SetScore> Sets,
    bool IsCorrection,
    DateTimeOffset SubmittedAt);

public sealed record SetScore(int SetNumber, int PlayerOneScore, int PlayerTwoScore);
