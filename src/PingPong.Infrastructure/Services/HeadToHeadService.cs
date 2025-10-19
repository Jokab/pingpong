using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class HeadToHeadService : IHeadToHeadService
{
    private readonly PingPongDbContext _context;

    public HeadToHeadService(PingPongDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<HeadToHeadRow>> GetHeadToHeadAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        // Load all events that involve the player, including players and sets
        // Note: We filter in the database but order in memory to support SQLite (DateTimeOffset ordering)
        var events = await _context.MatchEvents
            .AsNoTracking()
            .Include(e => e.PlayerOne)
            .Include(e => e.PlayerTwo)
            .Include(e => e.Sets)
            .Where(e => e.PlayerOneId == playerId || e.PlayerTwoId == playerId)
            .ToListAsync(cancellationToken);

        // Order in memory for SQLite compatibility
        events = events
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToList();

        if (events.Count == 0)
        {
            return Array.Empty<HeadToHeadRow>();
        }

        static string NormalizePair(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}-{b:N}" : $"{b:N}-{a:N}";

        // Group by date and normalized pair to build effective outcomes (last-write-wins per ordinal)
        var grouped = events
            .GroupBy(e => new { e.MatchDate, PairKey = NormalizePair(e.PlayerOneId, e.PlayerTwoId) })
            .Select(g => new { g.Key.MatchDate, g.Key.PairKey, Items = g.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList() })
            .ToList();

        var perOpponent = new Dictionary<Guid, MutableAggregate>();

        foreach (var group in grouped)
        {
            var ordered = group.Items;
            for (var ordinal = 0; ordinal < ordered.Count; ordinal++)
            {
                var ev = ordered[ordinal];
                var p1Sets = ev.Sets.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
                var p2Sets = ev.Sets.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
                if (p1Sets == p2Sets)
                {
                    continue; // ignore ties
                }

                var opponentId = ev.PlayerOneId == playerId ? ev.PlayerTwoId : ev.PlayerOneId;
                var opponentName = ev.PlayerOneId == playerId ? ev.PlayerTwo!.DisplayName : ev.PlayerOne!.DisplayName;

                if (!perOpponent.TryGetValue(opponentId, out var agg))
                {
                    agg = new MutableAggregate(opponentId, opponentName);
                    perOpponent[opponentId] = agg;
                }

                agg.MatchesPlayed++;
                var playerWon = (ev.PlayerOneId == playerId && p1Sets > p2Sets) || (ev.PlayerTwoId == playerId && p2Sets > p1Sets);
                if (playerWon) agg.Wins++; else agg.Losses++;

                // Point differential per match from player's perspective
                var pointDiff = ev.Sets.Sum(s =>
                    ev.PlayerOneId == playerId ? (s.PlayerOneScore - s.PlayerTwoScore) : (s.PlayerTwoScore - s.PlayerOneScore));
                agg.PointDifferentialTotal += pointDiff;
            }
        }

        var rows = perOpponent.Values
            .Select(a => new HeadToHeadRow(
                a.OpponentId,
                a.OpponentName,
                a.MatchesPlayed,
                a.Wins,
                a.Losses,
                a.MatchesPlayed == 0 ? 0d : Math.Round((double)a.Wins / a.MatchesPlayed, 4, MidpointRounding.AwayFromZero),
                a.MatchesPlayed == 0 ? 0d : Math.Round((double)a.PointDifferentialTotal / a.MatchesPlayed, 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(r => r.MatchesPlayed)
            .ThenByDescending(r => r.Wins)
            .ThenBy(r => r.OpponentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return rows;
    }

    public async Task<HeadToHeadDetails> GetHeadToHeadDetailsAsync(
        Guid playerAId,
        Guid playerBId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        if (playerAId == Guid.Empty || playerBId == Guid.Empty || playerAId == playerBId)
        {
            // Invalid or same player: return empty details with names if available
            var a = await _context.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerAId, cancellationToken);
            var b = await _context.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerBId, cancellationToken);
            return new HeadToHeadDetails(playerAId, a?.DisplayName ?? "", playerBId, b?.DisplayName ?? "", 0, 0, 0, 0, 0, 0d, 0d, null, null, Array.Empty<MatchHistoryEntry>());
        }

        var players = await _context.Players
            .AsNoTracking()
            .Where(p => p.Id == playerAId || p.Id == playerBId)
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(cancellationToken);

        var nameA = players.FirstOrDefault(p => p.Id == playerAId)?.DisplayName ?? "";
        var nameB = players.FirstOrDefault(p => p.Id == playerBId)?.DisplayName ?? "";

        // Query events for the pair within the optional date window
        var query = _context.MatchEvents
            .AsNoTracking()
            .Include(e => e.Sets)
            .Where(e => (e.PlayerOneId == playerAId && e.PlayerTwoId == playerBId) || (e.PlayerOneId == playerBId && e.PlayerTwoId == playerAId));

        if (from.HasValue)
        {
            query = query.Where(e => e.MatchDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.MatchDate <= to.Value);
        }

        // Load into memory first, then order for SQLite compatibility (DateTimeOffset ordering)
        var events = await query.ToListAsync(cancellationToken);
        events = events
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToList();

        if (events.Count == 0)
        {
            return new HeadToHeadDetails(playerAId, nameA, playerBId, nameB, 0, 0, 0, 0, 0, 0d, 0d, null, null, Array.Empty<MatchHistoryEntry>());
        }

        // Group by date to compute effective matches in chronological order for this pair
        var grouped = events
            .GroupBy(e => e.MatchDate)
            .Select(g => new { Date = g.Key, Items = g.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList() })
            .OrderBy(g => g.Date)
            .ToList();

        var effective = new List<(DateOnly Date, int Index, Domain.Entities.MatchEvent Ev)>();
        foreach (var g in grouped)
        {
            for (var i = 0; i < g.Items.Count; i++)
            {
                effective.Add((g.Date, i + 1, g.Items[i]));
            }
        }

        var matchesPlayed = 0;
        var wins = 0;
        var losses = 0;
        var setsWon = 0;
        var setsLost = 0;
        var pointDiffTotal = 0;

        var recent = new List<MatchHistoryEntry>();

        foreach (var tuple in effective)
        {
            var ev = tuple.Ev;
            var p1Sets = ev.Sets.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
            var p2Sets = ev.Sets.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
            if (p1Sets == p2Sets)
            {
                continue;
            }

            matchesPlayed++;
            var aWon = (ev.PlayerOneId == playerAId && p1Sets > p2Sets) || (ev.PlayerTwoId == playerAId && p2Sets > p1Sets);
            if (aWon) wins++; else losses++;

            if (ev.PlayerOneId == playerAId)
            {
                setsWon += p1Sets;
                setsLost += p2Sets;
            }
            else
            {
                setsWon += p2Sets;
                setsLost += p1Sets;
            }

            var pointDiff = ev.Sets.Sum(s => ev.PlayerOneId == playerAId ? (s.PlayerOneScore - s.PlayerTwoScore) : (s.PlayerTwoScore - s.PlayerOneScore));
            pointDiffTotal += pointDiff;

            // Build history entry for this effective match
            Guid? winnerId = null;
            string? winnerName = null;
            if (p1Sets != p2Sets)
            {
                var p1Wins = p1Sets > p2Sets;
                winnerId = p1Wins ? ev.PlayerOneId : ev.PlayerTwoId;
                winnerName = null; // Names not necessary here; UI can infer by id, or fill if needed
            }

            recent.Add(new MatchHistoryEntry(
                ev.Id,
                tuple.Date,
                tuple.Index,
                ev.PlayerOneId == playerAId ? nameA : nameB,
                ev.PlayerOneId == playerAId ? nameB : nameA,
                ev.Sets.Select(s => new SetPair(
                    ev.PlayerOneId == playerAId ? s.PlayerOneScore : s.PlayerTwoScore,
                    ev.PlayerOneId == playerAId ? s.PlayerTwoScore : s.PlayerOneScore)).ToList(),
                winnerId,
                winnerName,
                ev.SubmittedBy,
                ev.CreatedAt));
        }

        var lastMatch = effective.LastOrDefault();
        DateOnly? lastDate = null;
        Guid? lastWinnerId = null;
        if (lastMatch.Ev is not null)
        {
            lastDate = lastMatch.Date;
            var ev = lastMatch.Ev;
            var p1Sets = ev.Sets.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
            var p2Sets = ev.Sets.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
            if (p1Sets != p2Sets)
            {
                lastWinnerId = p1Sets > p2Sets ? ev.PlayerOneId : ev.PlayerTwoId;
            }
        }

        var winPct = matchesPlayed == 0 ? 0d : Math.Round((double)wins / matchesPlayed, 4, MidpointRounding.AwayFromZero);
        var avgPointDiff = matchesPlayed == 0 ? 0d : Math.Round((double)pointDiffTotal / matchesPlayed, 2, MidpointRounding.AwayFromZero);

        // Keep only the 5 most recent matches for the details view
        var recent5 = recent
            .OrderBy(r => r.MatchDate)
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.EventId)
            .TakeLast(5)
            .ToList();

        return new HeadToHeadDetails(
            playerAId,
            nameA,
            playerBId,
            nameB,
            matchesPlayed,
            wins,
            losses,
            setsWon,
            setsLost,
            winPct,
            avgPointDiff,
            lastDate,
            lastWinnerId,
            recent5);
    }

    private sealed class MutableAggregate
    {
        public MutableAggregate(Guid opponentId, string opponentName)
        {
            OpponentId = opponentId;
            OpponentName = opponentName;
        }

        public Guid OpponentId { get; }
        public string OpponentName { get; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int PointDifferentialTotal { get; set; }
    }
}


