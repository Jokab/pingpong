using PingPong.Application.Models;
using PingPong.Application.Players;
using PingPong.Domain.Players;

namespace PingPong.Application.Players;

public interface IPlayerDirectory
{
    Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
        string query,
        int size = 10,
        CancellationToken cancellationToken = default
    );

    Task<Player> AddPlayerAsync(string displayName, CancellationToken cancellationToken = default);

    Task<Player> EnsurePlayerAsync(
        string displayName,
        CancellationToken cancellationToken = default
    );
}
