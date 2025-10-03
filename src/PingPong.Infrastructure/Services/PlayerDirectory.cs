using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        var results = await _context.Players
            .Where(player => player.NormalizedName.Contains(normalized))
            .OrderBy(player => player.DisplayName)
            .Take(size)
            .Select(player => new PlayerSearchResult(
                player.Id,
                player.DisplayName,
                player.NormalizedName == normalized ? 1.0 : 0.5,
                player.NormalizedName == normalized))
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<Player> AddPlayerAsync(string displayName, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedKey = Player.NormalizeKey(displayName);

        var existing = await _context.Players
            .FirstOrDefaultAsync(player => player.NormalizedName == normalizedKey, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException($"Player '{displayName}' already exists.");
        }

        var player = Player.Create(displayName, now);
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
