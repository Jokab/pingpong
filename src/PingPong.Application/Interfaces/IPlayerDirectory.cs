using PingPong.Domain.Entities;
using PingPong.Application.Models;

namespace PingPong.Application.Interfaces;

public interface IPlayerDirectory
{
    Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(string query, int size = 10, CancellationToken cancellationToken = default);

    Task<Player> AddPlayerAsync(string displayName, CancellationToken cancellationToken = default);

    Task<Player> EnsurePlayerAsync(string displayName, CancellationToken cancellationToken = default);
}
