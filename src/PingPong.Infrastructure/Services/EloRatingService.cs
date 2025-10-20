using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PingPong.Application.Interfaces;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class EloRatingService : IRatingService
{
    private readonly PingPongDbContext _context;
    private readonly double _baseRating;
    private readonly double _kFactor;

    public EloRatingService(PingPongDbContext context, IConfiguration configuration)
    {
        _context = context;
        var baseRatingStr = configuration["Ratings:BaseRating"];
        var kFactorStr = configuration["Ratings:KFactor"];
        if (!double.TryParse(baseRatingStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _baseRating))
        {
            _baseRating = 1000d;
        }
        if (!double.TryParse(kFactorStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _kFactor))
        {
            _kFactor = 32d;
        }
    }

    public async Task RebuildAllRatingsAsync(CancellationToken cancellationToken = default)
    {
        // Load all players and initialize in-memory ratings
        var players = await _context.Players
            .AsNoTracking()
            .Select(p => new { p.Id })
            .ToListAsync(cancellationToken);

        var ratingByPlayerId = players.ToDictionary(p => p.Id, _ => _baseRating);

        // Load events needed to build effective match outcomes
        var events = await _context.MatchEvents
            .AsNoTracking()
            .Include(e => e.Sets)
            .ToListAsync(cancellationToken);

        // Order in-memory to support providers like SQLite that don't order by DateTimeOffset
        events = events
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToList();

        if (events.Count == 0)
        {
            // Ensure a PlayerRating row exists per player with base rating
            await UpsertRatingsAsync(ratingByPlayerId, DateTimeOffset.UtcNow, cancellationToken);
            return;
        }

        // Group by natural identity: date + normalized pair; within each group, chronological order defines ordinals
        static string NormalizePair(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}-{b:N}" : $"{b:N}-{a:N}";
        var grouped = events
            .GroupBy(e => new { e.MatchDate, PairKey = NormalizePair(e.PlayerOneId, e.PlayerTwoId) })
            .Select(g => new { g.Key.MatchDate, g.Key.PairKey, Items = g.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList() })
            .OrderBy(g => g.MatchDate)
            .ToList();

        DateTimeOffset lastAppliedAt = DateTimeOffset.UtcNow;

        foreach (var ordered in grouped.Select(group => group.Items))
        {
            foreach (var last in ordered)
            {
                // Determine sets won
                var p1Sets = last.Sets.Count(s => s.PlayerOneScore > s.PlayerTwoScore);
                var p2Sets = last.Sets.Count(s => s.PlayerTwoScore > s.PlayerOneScore);
                if (p1Sets == p2Sets) continue; // ignore invalid/draw

                var p1Id = last.PlayerOneId;
                var p2Id = last.PlayerTwoId;

                ratingByPlayerId.TryAdd(p1Id, _baseRating);
                ratingByPlayerId.TryAdd(p2Id, _baseRating);

                var p1Rating = ratingByPlayerId[p1Id];
                var p2Rating = ratingByPlayerId[p2Id];

                // Winner gets S=1, loser S=0
                var p1Won = p1Sets > p2Sets;
                var (p1New, p2New) = ComputeEloUpdate(p1Rating, p2Rating, p1Won);

                ratingByPlayerId[p1Id] = p1New;
                ratingByPlayerId[p2Id] = p2New;

                lastAppliedAt = last.CreatedAt;
            }
        }

        await UpsertRatingsAsync(ratingByPlayerId, lastAppliedAt, cancellationToken);
    }

    private (double newRa, double newRb) ComputeEloUpdate(double ra, double rb, bool aWins)
    {
        var ea = 1.0 / (1.0 + Math.Pow(10.0, (rb - ra) / 400.0));
        var eb = 1.0 / (1.0 + Math.Pow(10.0, (ra - rb) / 400.0));
        var sa = aWins ? 1.0 : 0.0;
        var sb = aWins ? 0.0 : 1.0;
        var raPrime = ra + _kFactor * (sa - ea);
        var rbPrime = rb + _kFactor * (sb - eb);
        // Round to 2 decimals for storage consistency
        raPrime = Math.Round(raPrime, 2, MidpointRounding.AwayFromZero);
        rbPrime = Math.Round(rbPrime, 2, MidpointRounding.AwayFromZero);
        return (raPrime, rbPrime);
    }

    private async Task UpsertRatingsAsync(Dictionary<Guid, double> ratingByPlayerId, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        // Load existing ratings WITH tracking so EF can manage updates
        var existing = await _context.PlayerRatings.ToListAsync(cancellationToken);
        var existingMap = existing.ToDictionary(r => r.PlayerId);

        foreach (var kvp in ratingByPlayerId)
        {
            if (existingMap.TryGetValue(kvp.Key, out var existingRating))
            {
                existingRating.CurrentRating = kvp.Value;
                existingRating.LastUpdatedAt = lastUpdatedAt;
            }
            else
            {
                _context.PlayerRatings.Add(new PlayerRating
                {
                    PlayerId = kvp.Key,
                    CurrentRating = kvp.Value,
                    LastUpdatedAt = lastUpdatedAt
                });
            }
        }

        // Remove ratings for players that no longer exist (defensive; should not happen under normal ops)
        var playerIds = ratingByPlayerId.Keys.ToHashSet();
        foreach (var r in existing.Where(r => !playerIds.Contains(r.PlayerId)))
        {
            _context.PlayerRatings.Remove(r);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}


