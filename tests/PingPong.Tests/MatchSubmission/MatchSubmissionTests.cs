using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PingPong.Api.Contracts.MatchSubmission;
using PingPong.Domain.Entities;
using PingPong.Domain.MatchSubmission;
using PingPong.Infrastructure.Persistence;
using PingPong.Tests.Support;

namespace PingPong.Tests.MatchSubmission;

public sealed class MatchSubmissionTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public MatchSubmissionTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubmitMatch_PersistsMatchEventAndPlayers()
    {
        // Arrange
        var client = _factory.CreateClient();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var request = (MatchSubmissionDto)
            new ScoredMatchSubmissionDto(
                "Alice",
                "Bob",
                today,
                new List<SetScoreDto> { new(11, 8), new(7, 11), new(11, 9) },
                "integration-test",
                TournamentFixtureId: null
            );

        // Act
        var response = await client.PostAsJsonAsync("/matches", request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Status {(int)response.StatusCode} {response.StatusCode}: {rawBody}"
        );

        var payload = await response.Content.ReadFromJsonAsync<MatchSubmissionResponse>();
        Assert.NotNull(payload);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PingPongDbContext>();
        var evt = await dbContext
            .MatchEvents.Include(e => e.Sets)
            .SingleAsync(e => e.Id == payload.EventId);

        Assert.Equal(today, evt.MatchDate);
        Assert.Equal(3, evt.Sets.Count);
        Assert.Equal("integration-test", evt.SubmittedBy);

        var players = await dbContext.Players.ToListAsync();
        Assert.Equal(2, players.Count);
    }

    [Fact]
    public async Task SubmitOutcomeOnlyMatch_PersistsOutcomeOnlyMatchEvent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var request = (MatchSubmissionDto)
            new OutcomeOnlyMatchSubmissionDto(
                "Charlie",
                "Diana",
                today,
                PlayerOneWon: true,
                Sets: null,
                "outcome-test",
                TournamentFixtureId: null
            );

        // Act
        var response = await client.PostAsJsonAsync("/matches", request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Status {(int)response.StatusCode} {response.StatusCode}: {rawBody}"
        );

        var payload = await response.Content.ReadFromJsonAsync<MatchSubmissionResponse>();
        Assert.NotNull(payload);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PingPongDbContext>();
        var evt = await dbContext
            .MatchEvents.Include(e => e.Sets)
            .SingleAsync(e => e.Id == payload.EventId);

        Assert.Equal(today, evt.MatchDate);
        Assert.Equal("outcome-test", evt.SubmittedBy);

        // Verify it's an OutcomeOnlyMatchEvent
        Assert.IsType<OutcomeOnlyMatchEvent>(evt);
        var outcomeEvent = (OutcomeOnlyMatchEvent)evt;
        Assert.True(outcomeEvent.PlayerOneWon);
        Assert.Empty(evt.Sets);

        var players = await dbContext.Players.ToListAsync();
        Assert.Contains(players, p => p.DisplayName == "Charlie");
        Assert.Contains(players, p => p.DisplayName == "Diana");

        // Verify the players are linked correctly
        Assert.Equal(evt.PlayerOneId, players.Single(p => p.DisplayName == "Charlie").Id);
        Assert.Equal(evt.PlayerTwoId, players.Single(p => p.DisplayName == "Diana").Id);
    }

    [Fact]
    public async Task SubmitOutcomeOnlyMatch_WithSetWinners_PersistsSets()
    {
        // Arrange
        var client = _factory.CreateClient();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var request = (MatchSubmissionDto)
            new OutcomeOnlyMatchSubmissionDto(
                "Eva",
                "Frank",
                today,
                PlayerOneWon: true,
                Sets: new List<SetWinnerDto> { new(1, true), new(2, false), new(3, true) },
                SubmittedBy: "outcome-sets",
                TournamentFixtureId: null
            );

        // Act
        var response = await client.PostAsJsonAsync("/matches", request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(
            response.IsSuccessStatusCode,
            $"Status {(int)response.StatusCode} {response.StatusCode}: {rawBody}"
        );

        var payload = await response.Content.ReadFromJsonAsync<MatchSubmissionResponse>();
        Assert.NotNull(payload);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PingPongDbContext>();
        var evt = await dbContext
            .MatchEvents.Include(e => e.Sets)
            .SingleAsync(e => e.Id == payload.EventId);

        var outcomeEvent = Assert.IsType<OutcomeOnlyMatchEvent>(evt);
        Assert.Equal(3, evt.Sets.Count);
        Assert.True(outcomeEvent.PlayerOneWon);
        Assert.All(evt.Sets, set => Assert.Null(set.PlayerOneScore));
        Assert.Equal(
            [true, false, true],
            evt.Sets.OrderBy(s => s.SetNumber).Select(s => s.PlayerOneWon!.Value)
        );
    }
}
