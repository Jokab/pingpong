using PingPong.Application.Models;

namespace PingPong.Application.History;

public interface IHistoryService
{
    Task<(IReadOnlyList<MatchHistoryEntry> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        Guid? playerId = null,
        CancellationToken cancellationToken = default
    );
}
