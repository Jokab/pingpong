using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Infrastructure.Persistence;

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
        var events = await _context.MatchEvents
            .AsNoTracking()
            .OrderBy(e => e.MatchDate)
            .Select(e => new EventProjection(
                e.PlayerOneId,
                e.PlayerTwoId,
                e.MatchDate,
                e.Sets.Select(s => new SetProjection(s.SetNumber, s.PlayerOneScore, s.PlayerTwoScore)).ToList(),
                e.CreatedAt,
                e.Id))
            .ToListAsync(cancellationToken);

        // Group by date + normalized pair, then assign ordinal and pick the last event per ordinal slot
        var comparer = StringComparer.Ordinal;
        var effectiveOutcomes = new List<Outcome>();
        var grouped = events
            .GroupBy(e => new { e.MatchDate, PairKey = NormalizePair(e.PlayerOneId, e.PlayerTwoId) })
            .Select(g => new { g.Key.MatchDate, g.Key.PairKey, Items = g.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList() })
            .ToList();

        foreach (var group in grouped)
        {
            // Assign ordinal by chronological item order for the pair on that date
            var ordered = group.Items;
            // For each ordinal position, take the last event (if multiple edits for same ordinal)
            for (var ordinal = 0; ordinal < ordered.Count; ordinal++)
            {
                // Collect all events for this ordinal (ordinal is index in chronological list by this pair/date)
                var forThisOrdinal = ordered.Where((_, idx) => idx == ordinal).ToList();
                if (forThisOrdinal.Count == 0) continue;
                var last = forThisOrdinal.Last();

                var p1Sets = last.Sets.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
                var p2Sets = last.Sets.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
                if (p1Sets == p2Sets) continue;

                effectiveOutcomes.Add(new Outcome(last.PlayerOneId, last.PlayerTwoId, p1Sets, p2Sets));
            }
        }

        foreach (var outcome in effectiveOutcomes)
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
            if (outcome.PlayerOneSetsWon > outcome.PlayerTwoSetsWon)
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

    private static string NormalizePair(Guid a, Guid b)
    {
        return a.CompareTo(b) < 0 ? $"{a:N}-{b:N}" : $"{b:N}-{a:N}";
    }

    private sealed record EventProjection(Guid PlayerOneId, Guid PlayerTwoId, DateOnly MatchDate, IReadOnlyList<SetProjection> Sets, DateTimeOffset CreatedAt, Guid Id);

    private sealed record SetProjection(int SetNumber, int PlayerOneScore, int PlayerTwoScore);

    private sealed record Outcome(Guid PlayerOneId, Guid PlayerTwoId, int PlayerOneSetsWon, int PlayerTwoSetsWon);

    private sealed class MutablePlayerStats
    {
        public int MatchesPlayed { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }
    }
}
