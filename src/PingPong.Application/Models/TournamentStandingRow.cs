namespace PingPong.Application.Models;

public sealed record TournamentStandingRow(
    Guid PlayerId,
    string PlayerName,
    int MatchesPlayed,
    int Wins,
    int Losses,
    int Points,
    double CurrentRating);

