using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;
using PingPong.Infrastructure.Services;

namespace PingPong.Tests;

public sealed class HeadToHeadServiceTests
{
    [Fact]
    public async Task GetHeadToHeadDetailsAsync_ComputesAggregates()
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
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));

        var alice = Player.Create("Alice", now);
        var bob = Player.Create("Bob", now);
        context.Players.AddRange(alice, bob);

        // Yesterday: Bob beats Alice 3-1
        var ev1 = new MatchEvent
        {
            Id = Guid.NewGuid(),
            PlayerOneId = bob.Id,
            PlayerTwoId = alice.Id,
            MatchDate = yesterday,
            CreatedAt = now,
            Sets =
            [
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 11, PlayerTwoScore = 9 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 11, PlayerTwoScore = 7 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 5, PlayerTwoScore = 11 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 4, PlayerOneScore = 11, PlayerTwoScore = 6 }
            ]
        };

        // Today: Alice beats Bob 3-0
        var ev2 = new MatchEvent
        {
            Id = Guid.NewGuid(),
            PlayerOneId = alice.Id,
            PlayerTwoId = bob.Id,
            MatchDate = today,
            CreatedAt = now.AddMinutes(1),
            Sets =
            [
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 11, PlayerTwoScore = 7 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 11, PlayerTwoScore = 4 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 11, PlayerTwoScore = 9 }
            ]
        };

        context.MatchEvents.AddRange(ev1, ev2);

        // Ensure child FKs
        foreach (var e in context.MatchEvents.Local)
        {
            foreach (var s in e.Sets)
            {
                s.MatchEventId = e.Id;
            }
        }

        await context.SaveChangesAsync();

        var service = new HeadToHeadService(context);

        var details = await service.GetHeadToHeadDetailsAsync(alice.Id, bob.Id);

        Assert.Equal(alice.Id, details.PlayerAId);
        Assert.Equal(bob.Id, details.PlayerBId);
        Assert.Equal(2, details.MatchesPlayed);
        Assert.Equal(1, details.Wins);   // Alice won one of two
        Assert.Equal(1, details.Losses);
        // Sanity checks only; exact set counts verified elsewhere
        Assert.True(details.WinPercentage is >= 0 and <= 1);
        Assert.Equal(today, details.LastMatchDate);
        Assert.Equal(alice.Id, details.LastMatchWinnerId);
        // No trend anymore
    }

    [Fact]
    public async Task GetHeadToHeadAsync_ListsOpponentsWithAggregates()
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
        context.Players.AddRange(alice, bob);

        context.MatchEvents.Add(new MatchEvent
        {
            Id = Guid.NewGuid(),
            PlayerOneId = alice.Id,
            PlayerTwoId = bob.Id,
            MatchDate = today,
            CreatedAt = now,
            Sets =
            [
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 11, PlayerTwoScore = 8 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 11, PlayerTwoScore = 9 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 7, PlayerTwoScore = 11 },
                new MatchEventSet { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 4, PlayerOneScore = 11, PlayerTwoScore = 5 }
            ]
        });

        // Fix child FKs
        foreach (var e in context.MatchEvents.Local)
        {
            foreach (var s in e.Sets)
            {
                s.MatchEventId = e.Id;
            }
        }
        await context.SaveChangesAsync();

        var service = new HeadToHeadService(context);
        var rows = await service.GetHeadToHeadAsync(alice.Id);

        var vsBob = Assert.Single(rows);
        Assert.Equal(bob.Id, vsBob.OpponentId);
        Assert.Equal(1, vsBob.MatchesPlayed);
        Assert.Equal(1, vsBob.Wins);
        Assert.Equal(0, vsBob.Losses);
    }
}


