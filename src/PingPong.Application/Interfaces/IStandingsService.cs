using PingPong.Application.Models;

namespace PingPong.Application.Interfaces;

public interface IStandingsService
{
    Task<IReadOnlyList<StandingRow>> GetStandingsAsync(
        CancellationToken cancellationToken = default
    );
}
