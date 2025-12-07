using PingPong.Application.Models;
using PingPong.Domain.Entities;

namespace PingPong.Application.Interfaces;

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
