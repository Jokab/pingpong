namespace PingPong.Application.Models;

public sealed record MatchHistoryEntry(
    Guid EventId,
    DateOnly MatchDate,
    int DayOrdinal,
    string PlayerOneName,
    string PlayerTwoName,
    IReadOnlyList<SetPair> Sets,
    Guid? WinnerPlayerId,
    string? WinnerName,
    string? SubmittedBy,
    DateTimeOffset CreatedAt);

public sealed record SetPair(int PlayerOneScore, int PlayerTwoScore);
