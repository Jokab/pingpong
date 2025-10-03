using PingPong.Application.Models;

namespace PingPong.Application.Interfaces;

public interface IHistoryService
{
    Task<IReadOnlyList<MatchHistoryEntry>> GetHistoryAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}
