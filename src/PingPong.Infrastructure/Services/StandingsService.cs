using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Infrastructure.Persistence;
using PingPong.Domain.ValueObjects;

namespace PingPong.Infrastructure.Services;

public sealed class StandingsService : IStandingsService
{
    private readonly PingPongDbContext _context;

    public StandingsService(PingPongDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StandingRow>> GetStandingsAsync(CancellationToken cancellationToken = default)
    {
        var players = await _context.Players
            .AsNoTracking()
            .Select(player => new PlayerProjection(
                player.Id,
                player.DisplayName,
                player.Rating != null ? player.Rating.CurrentRating : 0d))
            .ToListAsync(cancellationToken);

        if (players.Count == 0)
        {
            return Array.Empty<StandingRow>();
        }

        var playerStats = players.ToDictionary(
            player => player.Id,
            _ => new MutablePlayerStats());

        // Build effective matches from event log with last-write-wins per (date + normalized pair + ordinal)
        // SQLite doesn't support DateTimeOffset in ORDER BY, so order in memory
        var events = await _context.MatchEvents
            .AsNoTracking()
            .Include(e => e.Sets)
            .ToListAsync(cancellationToken);

        var outcomes = MatchOutcomeBuilder.BuildEffectiveOutcomes(events);

        foreach (var outcome in outcomes)
        {
            if (!playerStats.TryGetValue(outcome.PlayerOneId, out var p1))
            {
                p1 = new MutablePlayerStats();
                playerStats[outcome.PlayerOneId] = p1;
            }
            if (!playerStats.TryGetValue(outcome.PlayerTwoId, out var p2))
            {
                p2 = new MutablePlayerStats();
                playerStats[outcome.PlayerTwoId] = p2;
            }

            p1.MatchesPlayed++;
            p2.MatchesPlayed++;
            if (outcome.PlayerOneWon)
            {
                p1.Wins++;
                p2.Losses++;
            }
            else
            {
                p2.Wins++;
                p1.Losses++;
            }
        }

        var standings = players
            .Select(player =>
            {
                var stats = playerStats[player.Id];
                var matchesPlayed = stats.MatchesPlayed;
                var winPercentage = matchesPlayed == 0
                    ? 0d
                    : Math.Round((double)stats.Wins / matchesPlayed, 4, MidpointRounding.AwayFromZero);

                return new StandingRow(
                    player.Id,
                    player.DisplayName,
                    matchesPlayed,
                    stats.Wins,
                    stats.Losses,
                    winPercentage,
                    player.Rating);
            })
            .OrderByDescending(row => row.WinPercentage)
            .ThenByDescending(row => row.Wins)
            .ThenByDescending(row => row.MatchesPlayed)
            .ThenByDescending(row => row.CurrentRating)
            .ThenBy(row => row.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return standings;
    }

    private sealed record PlayerProjection(Guid Id, string DisplayName, double Rating);

    private sealed class MutablePlayerStats
    {
        public int MatchesPlayed { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }
    }
}
