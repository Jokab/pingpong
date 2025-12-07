namespace PingPong.Application.Tournaments;

public sealed record OpenFixtureOption(
    Guid FixtureId,
    Guid TournamentId,
    string TournamentName,
    Guid PlayerOneId,
    Guid PlayerTwoId,
    Guid OpponentId,
    string OpponentName
);
