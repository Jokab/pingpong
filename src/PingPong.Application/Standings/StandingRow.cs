namespace PingPong.Application.Standings;

public sealed record StandingRow(
    Guid PlayerId,
    string PlayerName,
    int MatchesPlayed,
    int Wins,
    int Losses,
    double WinPercentage,
    double CurrentRating
);
