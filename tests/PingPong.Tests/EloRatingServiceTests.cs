using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;
using PingPong.Infrastructure.Services;

namespace PingPong.Tests;

public sealed class EloRatingServiceTests
{
    private static IConfiguration BuildConfig(double baseRating = 1000, double kFactor = 32)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Ratings:BaseRating", baseRating.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "Ratings:KFactor", kFactor.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };
        return new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
    }

    [Fact]
    public async Task RebuildAllRatingsAsync_ComputesExpectedProgression()
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

        // Alice beats Bob 3-1
        context.MatchEvents.Add(new ScoredMatchEvent
        {
            Id = Guid.NewGuid(),
            PlayerOneId = alice.Id,
            PlayerTwoId = bob.Id,
            MatchDate = today,
            CreatedAt = now,
            EventType = MatchEventType.Recorded,
            Sets =
            [
                new MatchEventSetEntity { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 1, PlayerOneScore = 11, PlayerTwoScore = 7 },
                new MatchEventSetEntity { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 2, PlayerOneScore = 11, PlayerTwoScore = 9 },
                new MatchEventSetEntity { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 3, PlayerOneScore = 9, PlayerTwoScore = 11 },
                new MatchEventSetEntity { Id = Guid.NewGuid(), MatchEventId = Guid.Empty, SetNumber = 4, PlayerOneScore = 11, PlayerTwoScore = 8 }
            ]
        });

        // Ensure child FK is set
        foreach (var e in context.MatchEvents.Local)
        {
            foreach (var s in e.Sets)
            {
                s.MatchEventId = e.Id;
            }
        }

        await context.SaveChangesAsync();

        var service = new EloRatingService(context, BuildConfig());
        await service.RebuildAllRatingsAsync();

        var ratings = await context.PlayerRatings.AsNoTracking().ToListAsync();
        Assert.Equal(2, ratings.Count);

        var aliceRating = ratings.Single(r => r.PlayerId == alice.Id).CurrentRating;
        var bobRating = ratings.Single(r => r.PlayerId == bob.Id).CurrentRating;

        // With equal initial ratings and K=32, winner gains 16 points
        Assert.Equal(1016, aliceRating);
        Assert.Equal(984, bobRating);
    }
}


