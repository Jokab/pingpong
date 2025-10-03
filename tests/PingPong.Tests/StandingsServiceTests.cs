using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;
using PingPong.Infrastructure.Services;
using Xunit;

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

        context.Matches.AddRange(
            new Match
            {
                Id = Guid.NewGuid(),
                PlayerOneId = alice.Id,
                PlayerTwoId = bob.Id,
                MatchDate = today,
                Status = MatchStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                PlayerOneSetsWon = 3,
                PlayerTwoSetsWon = 1
            },
            new Match
            {
                Id = Guid.NewGuid(),
                PlayerOneId = alice.Id,
                PlayerTwoId = carol.Id,
                MatchDate = today,
                Status = MatchStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                PlayerOneSetsWon = 3,
                PlayerTwoSetsWon = 0
            },
            new Match
            {
                Id = Guid.NewGuid(),
                PlayerOneId = bob.Id,
                PlayerTwoId = carol.Id,
                MatchDate = today,
                Status = MatchStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                PlayerOneSetsWon = 0,
                PlayerTwoSetsWon = 3
            });

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
