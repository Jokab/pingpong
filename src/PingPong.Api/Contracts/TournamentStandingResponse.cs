namespace PingPong.Api.Contracts;

public sealed record TournamentStandingResponse(
    Guid PlayerId,
    string PlayerName,
    int MatchesPlayed,
    int Wins,
    int Losses,
    int Points,
    double Rating);


