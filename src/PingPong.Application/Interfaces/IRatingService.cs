using PingPong.Domain.Entities;

namespace PingPong.Application.Interfaces;

public interface IRatingService
{
    Task UpdateRatingsAsync(Match match, CancellationToken cancellationToken = default);
}
