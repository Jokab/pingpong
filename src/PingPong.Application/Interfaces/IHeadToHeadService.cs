using PingPong.Application.Models;

namespace PingPong.Application.Interfaces;

public interface IHeadToHeadService
{
    Task<IReadOnlyList<HeadToHeadRow>> GetHeadToHeadAsync(Guid playerId, CancellationToken cancellationToken = default);
}
