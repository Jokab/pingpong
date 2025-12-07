namespace PingPong.Api.Contracts.Tournaments;

public sealed class TournamentListResponse
{
    public List<TournamentSummaryResponse> Items { get; init; } = [];
}
