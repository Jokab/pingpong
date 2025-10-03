using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly PingPongDbContext _context;

    public HistoryService(PingPongDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<MatchHistoryEntry> Items, int TotalCount)> GetHistoryAsync(
        int page,
        int pageSize,
        Guid? playerId = null,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 25;

        var baseQuery = _context.MatchEvents
            .AsNoTracking()
            .Include(e => e.PlayerOne)
            .Include(e => e.PlayerTwo)
            .Include(e => e.Sets)
            .AsQueryable();

        if (playerId.HasValue && playerId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(e => e.PlayerOneId == playerId || e.PlayerTwoId == playerId);
        }

        // Order deterministically for ordinal and paging
        // SQLite cannot ORDER BY DateTimeOffset; avoid ordering by CreatedAt in SQL.
        var ordered = baseQuery
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.Id);

        var total = await baseQuery.CountAsync(cancellationToken);

        // Page window
        var pageItems = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new RawEvent(
                e.Id,
                e.PlayerOneId,
                e.PlayerTwoId,
                e.PlayerOne!.DisplayName,
                e.PlayerTwo!.DisplayName,
                e.MatchDate,
                e.CreatedAt,
                e.SubmittedBy,
                e.Sets
                    .OrderBy(s => s.SetNumber)
                    .Select(s => new SetTuple(s.PlayerOneScore, s.PlayerTwoScore))
                    .ToList()))
            .ToListAsync(cancellationToken);

        // Refine ordering in-memory to ensure chronological stability by CreatedAt, then Id.
        pageItems = pageItems
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToList();

        // Compute ordinals for page window relative to the day and pair across all events up to current event
        // To avoid scanning all rows, approximate ordinal by counting events with same date/pair with CreatedAt <= current.CreatedAt
        // This yields the correct 1-based index in chronological order.
        var items = new List<MatchHistoryEntry>(pageItems.Count);
        foreach (var ev in pageItems)
        {
            // SQLite/EF Core may not translate DateTimeOffset comparisons well in complex expressions.
            // Compute ordinal with a lighter SQL filter and finish comparison in-memory.
            var candidates = await _context.MatchEvents
                .AsNoTracking()
                .Where(x => x.MatchDate == ev.MatchDate)
                .Where(x => (x.PlayerOneId == ev.PlayerOneId && x.PlayerTwoId == ev.PlayerTwoId)
                            || (x.PlayerOneId == ev.PlayerTwoId && x.PlayerTwoId == ev.PlayerOneId))
                .Select(x => new { x.Id, x.CreatedAt })
                .ToListAsync(cancellationToken);

            var ordinal = candidates
                .Count(x => x.CreatedAt < ev.CreatedAt || (x.CreatedAt == ev.CreatedAt && x.Id.CompareTo(ev.Id) <= 0));

            var p1Sets = ev.Sets.Count(s => s.P1 > s.P2);
            var p2Sets = ev.Sets.Count(s => s.P2 > s.P1);
            string? winnerName = null;
            Guid? winnerId = null;
            if (p1Sets != p2Sets)
            {
                var p1Wins = p1Sets > p2Sets;
                winnerId = p1Wins ? ev.PlayerOneId : ev.PlayerTwoId;
                winnerName = p1Wins ? ev.PlayerOneName : ev.PlayerTwoName;
            }

            items.Add(new MatchHistoryEntry(
                ev.Id,
                ev.MatchDate,
                ordinal,
                ev.PlayerOneName,
                ev.PlayerTwoName,
                ev.Sets.Select(s => new SetPair(s.P1, s.P2)).ToList(),
                winnerId,
                winnerName,
                ev.SubmittedBy,
                ev.CreatedAt
            ));
        }

        return (items, total);
    }

    private static string NormalizePair(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}-{b:N}" : $"{b:N}-{a:N}";

    private sealed record RawEvent(
        Guid Id,
        Guid PlayerOneId,
        Guid PlayerTwoId,
        string PlayerOneName,
        string PlayerTwoName,
        DateOnly MatchDate,
        DateTimeOffset CreatedAt,
        string? SubmittedBy,
        IReadOnlyList<SetTuple> Sets);

    private sealed record SetTuple(int P1, int P2);
}
