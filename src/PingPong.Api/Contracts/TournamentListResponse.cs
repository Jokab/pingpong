namespace PingPong.Api.Contracts;

public sealed class TournamentListResponse
{
    public List<TournamentSummaryResponse> Items { get; init; } = [];
}


