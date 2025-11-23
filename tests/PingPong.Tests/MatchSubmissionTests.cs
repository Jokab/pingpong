using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PingPong.Api.Contracts;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;
using PingPong.Tests.Support;

namespace PingPong.Tests;

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

        var request = (MatchSubmissionDto)new ScoredMatchSubmissionDto(
            "Alice",
            "Bob",
            today,
            new List<SetScoreDto>
            {
                new(11, 8),
                new(7, 11),
                new(11, 9)
            },
            "integration-test");

        // Act
        var response = await client.PostAsJsonAsync("/matches", request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Status {(int)response.StatusCode} {response.StatusCode}: {rawBody}");

        var payload = await response.Content.ReadFromJsonAsync<MatchSubmissionResponse>();
        Assert.NotNull(payload);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PingPongDbContext>();
        var evt = await dbContext.MatchEvents
            .Include(e => e.Sets)
            .SingleAsync(e => e.Id == payload!.EventId);

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

        var request = (MatchSubmissionDto)new OutcomeOnlyMatchSubmissionDto(
            "Charlie",
            "Diana",
            today,
            PlayerOneWon: true,
            "outcome-test");

        // Act
        var response = await client.PostAsJsonAsync("/matches", request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Status {(int)response.StatusCode} {response.StatusCode}: {rawBody}");

        var payload = await response.Content.ReadFromJsonAsync<MatchSubmissionResponse>();
        Assert.NotNull(payload);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PingPongDbContext>();
        var evt = await dbContext.MatchEvents
            .Include(e => e.Sets)
            .SingleAsync(e => e.Id == payload!.EventId);

        Assert.Equal(today, evt.MatchDate);
        Assert.Equal("outcome-test", evt.SubmittedBy);
        
        // Verify it's an OutcomeOnlyMatchEvent
        Assert.IsType<OutcomeOnlyMatchEvent>(evt);
        var outcomeEvent = (OutcomeOnlyMatchEvent)evt;
        Assert.True(outcomeEvent.PlayerOneWon);
        Assert.Empty(evt.Sets); // Outcome-only events have no sets

        var players = await dbContext.Players.ToListAsync();
        Assert.Contains(players, p => p.DisplayName == "Charlie");
        Assert.Contains(players, p => p.DisplayName == "Diana");
        
        // Verify the players are linked correctly
        Assert.Equal(evt.PlayerOneId, players.Single(p => p.DisplayName == "Charlie").Id);
        Assert.Equal(evt.PlayerTwoId, players.Single(p => p.DisplayName == "Diana").Id);
    }
}
