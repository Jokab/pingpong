using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class PlayerDirectory : IPlayerDirectory
{
    private readonly PingPongDbContext _context;

    public PlayerDirectory(PingPongDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(string query, int size = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PlayerSearchResult>();
        }

        var trimmed = query.Trim();
        var normalized = Player.NormalizeKey(trimmed);

        var candidates = await _context.Players
            .Where(player => player.NormalizedName.Contains(normalized) || EF.Functions.Like(player.DisplayName, $"%{trimmed}%"))
            .Select(p => new { p.Id, p.DisplayName, p.NormalizedName })
            .ToListAsync(cancellationToken);

        var results = candidates
            .Select(p =>
            {
                var distance = Levenshtein(p.NormalizedName, normalized);
                var maxLen = Math.Max(p.NormalizedName.Length, normalized.Length);
                var baseScore = maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
                var isExactInsensitive = string.Equals(p.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase);
                var prefixBoost = p.NormalizedName.StartsWith(normalized) ? 0.1 : 0.0;
                var score = Math.Min(1.0, baseScore + prefixBoost + (isExactInsensitive ? 0.2 : 0));
                return new PlayerSearchResult(p.Id, p.DisplayName, score, isExactInsensitive || p.NormalizedName == normalized);
            })
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.DisplayName)
            .Take(size)
            .ToList();

        return results;

        static int Levenshtein(string a, string b)
        {
            var n = a.Length; var m = b.Length;
            var d = new int[n + 1, m + 1];
            for (var i = 0; i <= n; i++) d[i, 0] = i;
            for (var j = 0; j <= m; j++) d[0, j] = j;
            for (var i = 1; i <= n; i++)
            {
                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
    }

    public async Task<Player> AddPlayerAsync(string displayName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        var now = DateTimeOffset.UtcNow;
        var trimmed = Player.NormalizeDisplayName(displayName);

        // If exact display name exists (case-insensitive), append a random capital letter suffix
        var existsSameDisplay = await _context.Players
            .AnyAsync(p => p.DisplayName.ToUpper() == trimmed.ToUpper(), cancellationToken);

        var finalName = trimmed;
        if (existsSameDisplay)
        {
            var rng = new Random();
            for (var i = 0; i < 30; i++)
            {
                var letter = (char)('A' + rng.Next(0, 26));
                var candidate = $"{trimmed} {letter}";
                var taken = await _context.Players.AnyAsync(p => p.DisplayName.ToUpper() == candidate.ToUpper(), cancellationToken);
                if (!taken)
                {
                    finalName = candidate;
                    break;
                }
            }
        }

        var player = Player.Create(finalName, now);
        await _context.Players.AddAsync(player, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return player;
    }

    public async Task<Player> EnsurePlayerAsync(string displayName, CancellationToken cancellationToken = default)
    {
        var normalizedKey = Player.NormalizeKey(displayName);

        var existing = await _context.Players
            .FirstOrDefaultAsync(player => player.NormalizedName == normalizedKey, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var player = Player.Create(displayName, now);
        await _context.Players.AddAsync(player, cancellationToken);

        return player;
    }
}
