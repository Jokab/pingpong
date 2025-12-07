namespace PingPong.Application.Models;

public sealed record HeadToHeadRow(
    Guid OpponentId,
    string OpponentName,
    int MatchesPlayed,
    int Wins,
    int Losses,
    double WinPercentage,
    double AveragePointDifferential
);
