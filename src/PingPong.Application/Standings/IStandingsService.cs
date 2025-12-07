namespace PingPong.Application.Standings;

public interface IStandingsService
{
    Task<IReadOnlyList<StandingRow>> GetStandingsAsync(
        CancellationToken cancellationToken = default
    );
}
