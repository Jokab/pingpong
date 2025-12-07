using PingPong.Domain.Standings;
namespace PingPong.Domain.Players;

public sealed class Player
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PlayerAlias> Aliases { get; set; } = new List<PlayerAlias>();

    public PlayerRating? Rating { get; set; }

    public static Player Create(string displayName, DateTimeOffset createdAt)
    {
        var cleanedDisplayName = NormalizeDisplayName(displayName);
        var normalizedName = NormalizeKey(displayName);

        return new Player
        {
            Id = Guid.NewGuid(),
            DisplayName = cleanedDisplayName,
            NormalizedName = normalizedName,
            CreatedAt = createdAt,
        };
    }

    public static string NormalizeDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var trimmed = displayName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException(
                "Player display name cannot be empty.",
                nameof(displayName)
            );
        }

        return trimmed;
    }

    public static string NormalizeKey(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var trimmed = displayName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException(
                "Player display name cannot be empty.",
                nameof(displayName)
            );
        }

        return trimmed.ToUpperInvariant();
    }
}
