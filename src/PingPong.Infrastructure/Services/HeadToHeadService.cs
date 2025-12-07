using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.MatchSubmission;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class HeadToHeadService : IHeadToHeadService
{
    private readonly PingPongDbContext _context;

    public HeadToHeadService(PingPongDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<HeadToHeadRow>> GetHeadToHeadAsync(
        Guid playerId,
        CancellationToken cancellationToken = default
    )
    {
        // Load all events that involve the player, including players and sets
        // Note: We filter in the database but order in memory to support SQLite (DateTimeOffset ordering)
        var events = await _context
            .MatchEvents.AsNoTracking()
            .Include(e => e.PlayerOne)
            .Include(e => e.PlayerTwo)
            .Include(e => e.Sets)
            .Where(e => e.PlayerOneId == playerId || e.PlayerTwoId == playerId)
            .ToListAsync(cancellationToken);

        var eventLookup = events.ToDictionary(e => e.Id);
        var outcomes = MatchOutcomeBuilder
            .BuildEffectiveOutcomes(events)
            .Where(o => o.PlayerOneId == playerId || o.PlayerTwoId == playerId)
            .OrderBy(o => o.MatchDate)
            .ThenBy(o => o.CreatedAt)
            .ThenBy(o => o.EventId)
            .ToList();

        if (outcomes.Count == 0)
        {
            return Array.Empty<HeadToHeadRow>();
        }

        var perOpponent = new Dictionary<Guid, MutableAggregate>();

        foreach (var outcome in outcomes)
        {
            if (!eventLookup.TryGetValue(outcome.EventId, out var ev))
            {
                continue;
            }

            var playerIsP1 = outcome.PlayerOneId == playerId;
            var opponentId = playerIsP1 ? outcome.PlayerTwoId : outcome.PlayerOneId;
            var opponentName = playerIsP1 ? ev.PlayerTwo!.DisplayName : ev.PlayerOne!.DisplayName;

            if (!perOpponent.TryGetValue(opponentId, out var agg))
            {
                agg = new MutableAggregate(opponentId, opponentName);
                perOpponent[opponentId] = agg;
            }

            agg.MatchesPlayed++;
            var playerWon = playerIsP1 ? outcome.PlayerOneWon : !outcome.PlayerOneWon;
            if (playerWon)
            {
                agg.Wins++;
            }
            else
            {
                agg.Losses++;
            }

            agg.PointDifferentialTotal += ComputePointDifferential(outcome, playerIsP1);
        }

        var rows = perOpponent
            .Values.Select(a => new HeadToHeadRow(
                a.OpponentId,
                a.OpponentName,
                a.MatchesPlayed,
                a.Wins,
                a.Losses,
                a.MatchesPlayed == 0
                    ? 0d
                    : Math.Round(
                        (double)a.Wins / a.MatchesPlayed,
                        4,
                        MidpointRounding.AwayFromZero
                    ),
                a.MatchesPlayed == 0
                    ? 0d
                    : Math.Round(
                        (double)a.PointDifferentialTotal / a.MatchesPlayed,
                        2,
                        MidpointRounding.AwayFromZero
                    )
            ))
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
        CancellationToken cancellationToken = default
    )
    {
        if (playerAId == Guid.Empty || playerBId == Guid.Empty || playerAId == playerBId)
        {
            // Invalid or same player: return empty details with names if available
            var a = await _context
                .Players.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == playerAId, cancellationToken);
            var b = await _context
                .Players.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == playerBId, cancellationToken);
            return new HeadToHeadDetails(
                playerAId,
                a?.DisplayName ?? "",
                playerBId,
                b?.DisplayName ?? "",
                0,
                0,
                0,
                0,
                0,
                0d,
                0d,
                null,
                null,
                Array.Empty<MatchHistoryEntry>()
            );
        }

        var players = await _context
            .Players.AsNoTracking()
            .Where(p => p.Id == playerAId || p.Id == playerBId)
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(cancellationToken);

        var nameA = players.FirstOrDefault(p => p.Id == playerAId)?.DisplayName ?? "";
        var nameB = players.FirstOrDefault(p => p.Id == playerBId)?.DisplayName ?? "";

        var query = _context
            .MatchEvents.AsNoTracking()
            .Include(e => e.Sets)
            .Where(e =>
                (e.PlayerOneId == playerAId && e.PlayerTwoId == playerBId)
                || (e.PlayerOneId == playerBId && e.PlayerTwoId == playerAId)
            );

        if (from.HasValue)
        {
            query = query.Where(e => e.MatchDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.MatchDate <= to.Value);
        }

        var events = await query
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        events = events
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToList();

        if (events.Count == 0)
        {
            return new HeadToHeadDetails(
                playerAId,
                nameA,
                playerBId,
                nameB,
                0,
                0,
                0,
                0,
                0,
                0d,
                0d,
                null,
                null,
                Array.Empty<MatchHistoryEntry>()
            );
        }

        var eventLookup = events.ToDictionary(e => e.Id);
        var outcomes = MatchOutcomeBuilder
            .BuildEffectiveOutcomes(events)
            .Where(o =>
                (o.PlayerOneId == playerAId && o.PlayerTwoId == playerBId)
                || (o.PlayerOneId == playerBId && o.PlayerTwoId == playerAId)
            )
            .OrderBy(o => o.MatchDate)
            .ThenBy(o => o.CreatedAt)
            .ThenBy(o => o.EventId)
            .ToList();

        if (outcomes.Count == 0)
        {
            return new HeadToHeadDetails(
                playerAId,
                nameA,
                playerBId,
                nameB,
                0,
                0,
                0,
                0,
                0,
                0d,
                0d,
                null,
                null,
                Array.Empty<MatchHistoryEntry>()
            );
        }

        var ordinalByEvent = BuildOrdinals(outcomes);

        var matchesPlayed = 0;
        var wins = 0;
        var losses = 0;
        var setsWon = 0;
        var setsLost = 0;
        var pointDiffTotal = 0;
        var recent = new List<MatchHistoryEntry>(outcomes.Count);

        foreach (var outcome in outcomes)
        {
            matchesPlayed++;
            var playerAWon =
                outcome.PlayerOneId == playerAId ? outcome.PlayerOneWon : !outcome.PlayerOneWon;
            if (playerAWon)
            {
                wins++;
            }
            else
            {
                losses++;
            }

            var (setsWonDelta, setsLostDelta) = CountSetsForPlayer(outcome, playerAId);
            setsWon += setsWonDelta;
            setsLost += setsLostDelta;

            pointDiffTotal += ComputePointDifferential(outcome, outcome.PlayerOneId == playerAId);

            if (!eventLookup.TryGetValue(outcome.EventId, out var ev))
            {
                continue;
            }

            var playerOneName = ev.PlayerOneId == playerAId ? nameA : nameB;
            var playerTwoName = ev.PlayerOneId == playerAId ? nameB : nameA;
            var setPairs = BuildSetPairsForPlayer(outcome, playerAId);
            var winnerId = outcome.WinnerId;
            string? winnerName =
                winnerId == playerAId ? nameA
                : winnerId == playerBId ? nameB
                : null;

            recent.Add(
                new MatchHistoryEntry(
                    outcome.EventId,
                    outcome.MatchDate,
                    ordinalByEvent[outcome.EventId],
                    playerOneName,
                    playerTwoName,
                    setPairs,
                    winnerId,
                    winnerName,
                    ev.SubmittedBy,
                    outcome.CreatedAt
                )
            );
        }

        var lastOutcome = outcomes.Last();
        var winPct =
            matchesPlayed == 0
                ? 0d
                : Math.Round((double)wins / matchesPlayed, 4, MidpointRounding.AwayFromZero);
        var avgPointDiff =
            matchesPlayed == 0
                ? 0d
                : Math.Round(
                    (double)pointDiffTotal / matchesPlayed,
                    2,
                    MidpointRounding.AwayFromZero
                );
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
            lastOutcome.MatchDate,
            lastOutcome.WinnerId,
            recent5
        );
    }

    private static Dictionary<Guid, int> BuildOrdinals(IReadOnlyList<MatchOutcome> outcomes)
    {
        var map = new Dictionary<Guid, int>();
        var grouped = outcomes.GroupBy(o => o.MatchDate).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var ordinal = 1;
            foreach (var outcome in group.OrderBy(o => o.CreatedAt).ThenBy(o => o.EventId))
            {
                map[outcome.EventId] = ordinal++;
            }
        }

        return map;
    }

    private static (int setsWon, int setsLost) CountSetsForPlayer(
        MatchOutcome outcome,
        Guid playerId
    )
    {
        var playerIsP1 = outcome.PlayerOneId == playerId;
        var setsWon = outcome.Sets.Count(s => playerIsP1 ? s.PlayerOneWon : !s.PlayerOneWon);
        var setsLost = outcome.Sets.Count - setsWon;
        return (setsWon, setsLost);
    }

    private static int ComputePointDifferential(MatchOutcome outcome, bool perspectiveIsPlayerOne)
    {
        var diff = 0;
        foreach (var set in outcome.Sets)
        {
            if (set is ScoredMatchSetResult scored)
            {
                diff += perspectiveIsPlayerOne
                    ? scored.Score.PlayerOneScore - scored.Score.PlayerTwoScore
                    : scored.Score.PlayerTwoScore - scored.Score.PlayerOneScore;
            }
        }

        return diff;
    }

    private static IReadOnlyList<SetPair> BuildSetPairsForPlayer(
        MatchOutcome outcome,
        Guid playerId
    )
    {
        var playerIsP1 = outcome.PlayerOneId == playerId;
        var pairs = new List<SetPair>(outcome.Sets.Count);

        foreach (var set in outcome.Sets)
        {
            if (set is ScoredMatchSetResult scored)
            {
                var p1Score = playerIsP1
                    ? scored.Score.PlayerOneScore
                    : scored.Score.PlayerTwoScore;
                var p2Score = playerIsP1
                    ? scored.Score.PlayerTwoScore
                    : scored.Score.PlayerOneScore;
                pairs.Add(new SetPair(p1Score, p2Score));
            }
            else
            {
                var playerWon = playerIsP1 ? set.PlayerOneWon : !set.PlayerOneWon;
                pairs.Add(playerWon ? new SetPair(1, 0) : new SetPair(0, 1));
            }
        }

        return pairs;
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
