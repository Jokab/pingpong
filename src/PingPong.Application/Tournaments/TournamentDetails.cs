namespace PingPong.Application.Tournaments;

public sealed record TournamentDetails(
    TournamentSummary Summary,
    IReadOnlyList<TournamentStandingRow> Standings,
    IReadOnlyList<TournamentFixtureView> Fixtures
);
