namespace PingPong.Application.Models;

public sealed record HeadToHeadDetails(
    Guid PlayerAId,
    string PlayerAName,
    Guid PlayerBId,
    string PlayerBName,
    int MatchesPlayed,
    int Wins,
    int Losses,
    int SetsWon,
    int SetsLost,
    double WinPercentage,
    double AveragePointDifferential,
    DateOnly? LastMatchDate,
    Guid? LastMatchWinnerId,
    IReadOnlyList<MatchHistoryEntry> RecentMatches);


