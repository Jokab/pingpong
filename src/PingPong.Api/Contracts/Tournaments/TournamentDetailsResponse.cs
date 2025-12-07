namespace PingPong.Api.Contracts.Tournaments;

public sealed class TournamentDetailsResponse
{
    public TournamentSummaryResponse? Summary { get; init; }

    public List<TournamentStandingResponse> Standings { get; init; } = [];

    public List<TournamentFixtureResponse> Fixtures { get; init; } = [];
}
