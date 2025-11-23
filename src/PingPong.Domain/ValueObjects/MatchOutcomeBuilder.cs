using PingPong.Domain.Entities;
using PingPong.Domain.Exceptions;

namespace PingPong.Domain.ValueObjects;

public static class MatchOutcomeBuilder
{
    extension(MatchEvent matchEvent)
    {
        public MatchOutcome ToOutcome()
        {
            ArgumentNullException.ThrowIfNull(matchEvent);

            var sets = BuildSets(matchEvent);

            bool? matchWinner = matchEvent switch
            {
                OutcomeOnlyMatchEvent outcomeOnly => outcomeOnly.PlayerOneWon,
                _ => null
            };

            return MatchOutcome.Create(
                matchEvent.Id,
                matchEvent.PlayerOneId,
                matchEvent.PlayerTwoId,
                matchEvent.MatchDate,
                matchWinner,
                sets,
                matchEvent.SubmittedBy,
                matchEvent.CreatedAt);
        }
        public bool TryToOutcome(out MatchOutcome? outcome)
        {
            try
            {
                outcome = matchEvent.ToOutcome();
                return true;
            }
            catch (DomainValidationException)
            {
                outcome = null;
                return false;
            }
        }
    }

    public static IReadOnlyList<MatchOutcome> BuildEffectiveOutcomes(IEnumerable<MatchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var list = events
            .OrderBy(e => e.MatchDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToList();

        if (list.Count == 0)
        {
            return [];
        }

        var results = new List<MatchOutcome>();

        var grouped = list
            .GroupBy(e => new { e.MatchDate, Pair = NormalizePair(e.PlayerOneId, e.PlayerTwoId) })
            .Select(g => new { g.Key.MatchDate, g.Key.Pair, Items = g.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList() })
            .OrderBy(g => g.MatchDate)
            .ThenBy(g => g.Pair, StringComparer.Ordinal)
            .ToList();

        foreach (var group in grouped)
        {
            var effectiveByOrdinal = new Dictionary<int, MatchEvent>();
            for (var ordinal = 0; ordinal < group.Items.Count; ordinal++)
            {
                effectiveByOrdinal[ordinal] = group.Items[ordinal];
            }

            foreach (var ordinal in effectiveByOrdinal.Keys.OrderBy(o => o))
            {
                if (effectiveByOrdinal[ordinal].TryToOutcome(out var outcome) && outcome is not null)
                {
                    results.Add(outcome);
                }
            }
        }

        return results;

        static string NormalizePair(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}-{b:N}" : $"{b:N}-{a:N}";
    }

    private static IReadOnlyList<MatchSetResult> BuildSets(MatchEvent matchEvent)
    {
        if (matchEvent is OutcomeOnlyMatchEvent outcomeOnly)
        {
            if (matchEvent.Sets.Count != 0)
            {
                return matchEvent.Sets
                    .OrderBy(s => s.SetNumber)
                    .Select(s => s.ToSetResult())
                    .ToList();
            }

            return
            [
                new OutcomeOnlyMatchSetResult(1, outcomeOnly.PlayerOneWon)
            ];
        }

        if (matchEvent.Sets.Count == 0)
        {
            throw new DomainValidationException("Scored match event must have at least one set.");
        }

        return matchEvent.Sets
            .OrderBy(s => s.SetNumber)
            .Select(s => s.ToSetResult())
            .ToList();
    }
}

