namespace PingPong.Application.Standings;

public interface IRatingService
{
    Task RebuildAllRatingsAsync(CancellationToken cancellationToken = default);
}
