using PingPong.Domain.Players;
namespace PingPong.Domain.Standings;

public sealed class PlayerRating
{
    public Guid PlayerId { get; set; }

    public Player? Player { get; set; }

    public double CurrentRating { get; set; }

    public DateTimeOffset LastUpdatedAt { get; set; }
}
