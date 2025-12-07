namespace PingPong.Api.Contracts;

public sealed class TournamentDetailsResponse
{
    public TournamentSummaryResponse? Summary { get; init; }

    public List<TournamentStandingResponse> Standings { get; init; } = [];

    public List<TournamentFixtureResponse> Fixtures { get; init; } = [];
}
