namespace PingPong.Domain.Players;

public sealed class PlayerAlias
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Player? Player { get; set; }

    public string AliasName { get; set; } = string.Empty;

    public double ConfidenceScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
