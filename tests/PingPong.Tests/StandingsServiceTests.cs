using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;
using PingPong.Infrastructure.Services;

namespace PingPong.Tests;

public sealed class StandingsServiceTests
{
    [Fact]
    public async Task GetStandingsAsync_ComputesAndOrdersRows()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PingPongDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PingPongDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var alice = Player.Create("Alice", now);
        var bob = Player.Create("Bob", now);
        var carol = Player.Create("Carol", now);
        var dave = Player.Create("Dave", now);

        context.Players.AddRange(alice, bob, carol, dave);

        context.PlayerRatings.AddRange(
            new PlayerRating { PlayerId = alice.Id, CurrentRating = 1050, LastUpdatedAt = now },
            new PlayerRating { PlayerId = bob.Id, CurrentRating = 990, LastUpdatedAt = now },
            new PlayerRating { PlayerId = carol.Id, CurrentRating = 1010, LastUpdatedAt = now },
            new PlayerRating { PlayerId = dave.Id, CurrentRating = 950, LastUpdatedAt = now });

        context.MatchEvents.AddRange(
            new ScoredMatchEvent
            {
                Id = Guid.NewGuid(),
                PlayerOneId = alice.Id,
                PlayerTwoId = bob.Id,
                MatchDate = today,
                CreatedAt = now,
                EventType = MatchEventType.Recorded,
                Sets =
                [
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 11, PlayerTwoScore = 7 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 11, PlayerTwoScore = 9 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 9, PlayerTwoScore = 11 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 4, PlayerOneScore = 11, PlayerTwoScore = 8 }
                ]
            },
            new ScoredMatchEvent
            {
                Id = Guid.NewGuid(),
                PlayerOneId = alice.Id,
                PlayerTwoId = carol.Id,
                MatchDate = today,
                CreatedAt = now.AddMinutes(1),
                EventType = MatchEventType.Recorded,
                Sets =
                [
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 11, PlayerTwoScore = 3 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 11, PlayerTwoScore = 6 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 11, PlayerTwoScore = 4 }
                ]
            },
            new ScoredMatchEvent
            {
                Id = Guid.NewGuid(),
                PlayerOneId = bob.Id,
                PlayerTwoId = carol.Id,
                MatchDate = today,
                CreatedAt = now.AddMinutes(2),
                EventType = MatchEventType.Recorded,
                Sets =
                [
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 5, PlayerTwoScore = 11 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 7, PlayerTwoScore = 11 },
                    new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 9, PlayerTwoScore = 11 }
                ]
            });

        // Ensure MatchEventId is set for child sets (simulate EF cascade keying)
        foreach (var e in context.MatchEvents.Local)
        {
            foreach (var s in e.Sets)
            {
                s.MatchEventId = e.Id;
            }
        }
        await context.SaveChangesAsync();

        var service = new StandingsService(context);

        var standings = await service.GetStandingsAsync();

        Assert.Equal(4, standings.Count);

        Assert.Collection(
            standings,
            first => Assert.Equal(alice.Id, first.PlayerId),
            second => Assert.Equal(carol.Id, second.PlayerId),
            third => Assert.Equal(bob.Id, third.PlayerId),
            fourth => Assert.Equal(dave.Id, fourth.PlayerId));

        var aliceRow = standings.First(row => row.PlayerId == alice.Id);
        Assert.Equal(2, aliceRow.MatchesPlayed);
        Assert.Equal(2, aliceRow.Wins);
        Assert.Equal(0, aliceRow.Losses);
        Assert.Equal(1d, aliceRow.WinPercentage);
        Assert.Equal(1050, aliceRow.CurrentRating);

        var carolRow = standings.First(row => row.PlayerId == carol.Id);
        Assert.Equal(2, carolRow.MatchesPlayed);
        Assert.Equal(1, carolRow.Wins);
        Assert.Equal(1, carolRow.Losses);
        Assert.Equal(0.5d, carolRow.WinPercentage);
        Assert.Equal(1010, carolRow.CurrentRating);

        var bobRow = standings.First(row => row.PlayerId == bob.Id);
        Assert.Equal(2, bobRow.MatchesPlayed);
        Assert.Equal(0, bobRow.Wins);
        Assert.Equal(2, bobRow.Losses);
        Assert.Equal(0d, bobRow.WinPercentage);
        Assert.Equal(990, bobRow.CurrentRating);

        var daveRow = standings.First(row => row.PlayerId == dave.Id);
        Assert.Equal(0, daveRow.MatchesPlayed);
        Assert.Equal(0, daveRow.Wins);
        Assert.Equal(0, daveRow.Losses);
        Assert.Equal(0d, daveRow.WinPercentage);
        Assert.Equal(950, daveRow.CurrentRating);
    }
}
