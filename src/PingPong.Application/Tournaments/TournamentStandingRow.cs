namespace PingPong.Application.Tournaments;

public sealed record TournamentStandingRow(
    Guid PlayerId,
    string PlayerName,
    int MatchesPlayed,
    int Wins,
    int Losses,
    int Points,
    double CurrentRating
);
