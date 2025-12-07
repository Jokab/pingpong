namespace PingPong.Application.Tournaments;

public sealed record CreateTournamentRequest(
    string Name,
    string? Description,
    int DurationDays,
    int PointsPerWin = 1
);
