namespace PingPong.Application.Interfaces;

public interface IRatingService
{
    Task RebuildAllRatingsAsync(CancellationToken cancellationToken = default);
}
