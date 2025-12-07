using PingPong.Domain.Entities;
using PingPong.Domain.ValueObjects;

namespace PingPong.Tests;

public sealed class MatchOutcomeBuilderTests
{
    [Fact]
    public void ToOutcome_ScoredMatch_ProducesScoredSets()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        var ev = new ScoredMatchEvent
        {
            Id = matchId,
            PlayerOneId = p1,
            PlayerTwoId = p2,
            MatchDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
            Sets =
            [
                MatchEventSetEntity.CreateScored(matchId, 1, new MatchSetScore(11, 7)),
                MatchEventSetEntity.CreateScored(matchId, 2, new MatchSetScore(6, 11)),
                MatchEventSetEntity.CreateScored(matchId, 3, new MatchSetScore(11, 9)),
            ],
        };

        var outcome = ev.ToOutcome();

        Assert.True(outcome.PlayerOneWon);
        Assert.Equal(3, outcome.Sets.Count);
        Assert.All(outcome.Sets, set => Assert.IsType<ScoredMatchSetResult>(set));
    }

    [Fact]
    public void ToOutcome_OutcomeOnlyWithSets_ProducesOutcomeOnlySetResults()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        var ev = new OutcomeOnlyMatchEvent
        {
            Id = matchId,
            PlayerOneId = p1,
            PlayerTwoId = p2,
            MatchDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
            PlayerOneWon = true,
            Sets =
            [
                MatchEventSetEntity.CreateOutcomeOnly(matchId, 1, true),
                MatchEventSetEntity.CreateOutcomeOnly(matchId, 2, false),
                MatchEventSetEntity.CreateOutcomeOnly(matchId, 3, true),
            ],
        };

        var outcome = ev.ToOutcome();

        Assert.True(outcome.PlayerOneWon);
        Assert.Equal(3, outcome.Sets.Count);
        Assert.All(outcome.Sets, set => Assert.IsType<OutcomeOnlyMatchSetResult>(set));
    }

    [Fact]
    public void ToOutcome_OutcomeOnlyWithoutSets_SynthesizesSingleSet()
    {
        var ev = new OutcomeOnlyMatchEvent
        {
            Id = Guid.NewGuid(),
            PlayerOneId = Guid.NewGuid(),
            PlayerTwoId = Guid.NewGuid(),
            MatchDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
            PlayerOneWon = false,
        };

        var outcome = ev.ToOutcome();

        Assert.False(outcome.PlayerOneWon);
        var set = Assert.Single(outcome.Sets);
        Assert.IsType<OutcomeOnlyMatchSetResult>(set);
        Assert.False(set.PlayerOneWon);
    }
}
