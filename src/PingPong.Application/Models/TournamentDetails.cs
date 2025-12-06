namespace PingPong.Application.Models;

public sealed record TournamentDetails(
    TournamentSummary Summary,
    IReadOnlyList<TournamentStandingRow> Standings,
    IReadOnlyList<TournamentFixtureView> Fixtures);

