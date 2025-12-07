using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PingPong.Application.Interfaces;
using PingPong.Domain.Entities;
using PingPong.Domain.MatchSubmission;
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
        if (
            !double.TryParse(
                baseRatingStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _baseRating
            )
        )
        {
            _baseRating = 1000d;
        }
        if (
            !double.TryParse(
                kFactorStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _kFactor
            )
        )
        {
            _kFactor = 32d;
        }
    }

    public async Task RebuildAllRatingsAsync(CancellationToken cancellationToken = default)
    {
        // Load all players and initialize in-memory ratings
        var players = await _context
            .Players.AsNoTracking()
            .Select(p => new { p.Id })
            .ToListAsync(cancellationToken);

        var ratingByPlayerId = players.ToDictionary(p => p.Id, _ => _baseRating);

        // Load events needed to build effective match outcomes
        var events = await _context
            .MatchEvents.AsNoTracking()
            .Include(e => e.Sets)
            .ToListAsync(cancellationToken);
        var outcomes = MatchOutcomeBuilder
            .BuildEffectiveOutcomes(events)
            .OrderBy(o => o.MatchDate)
            .ThenBy(o => o.CreatedAt)
            .ThenBy(o => o.EventId)
            .ToList();

        if (outcomes.Count == 0)
        {
            await UpsertRatingsAsync(ratingByPlayerId, DateTimeOffset.UtcNow, cancellationToken);
            return;
        }

        DateTimeOffset lastAppliedAt = DateTimeOffset.UtcNow;

        foreach (var outcome in outcomes)
        {
            var p1Id = outcome.PlayerOneId;
            var p2Id = outcome.PlayerTwoId;

            ratingByPlayerId.TryAdd(p1Id, _baseRating);
            ratingByPlayerId.TryAdd(p2Id, _baseRating);

            var p1Rating = ratingByPlayerId[p1Id];
            var p2Rating = ratingByPlayerId[p2Id];

            var (p1New, p2New) = ComputeEloUpdate(p1Rating, p2Rating, outcome.PlayerOneWon);

            ratingByPlayerId[p1Id] = p1New;
            ratingByPlayerId[p2Id] = p2New;

            lastAppliedAt = outcome.CreatedAt;
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

    private async Task UpsertRatingsAsync(
        Dictionary<Guid, double> ratingByPlayerId,
        DateTimeOffset lastUpdatedAt,
        CancellationToken cancellationToken
    )
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
                _context.PlayerRatings.Add(
                    new PlayerRating
                    {
                        PlayerId = kvp.Key,
                        CurrentRating = kvp.Value,
                        LastUpdatedAt = lastUpdatedAt,
                    }
                );
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
