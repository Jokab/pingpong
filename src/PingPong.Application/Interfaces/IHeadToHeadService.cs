using PingPong.Application.Models;

namespace PingPong.Application.Interfaces;

public interface IHeadToHeadService
{
    Task<IReadOnlyList<HeadToHeadRow>> GetHeadToHeadAsync(Guid playerId, CancellationToken cancellationToken = default);

    Task<HeadToHeadDetails> GetHeadToHeadDetailsAsync(
        Guid playerAId,
        Guid playerBId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}
