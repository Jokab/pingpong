using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PingPong.Api.Contracts;
using PingPong.Tests.Support;
using Xunit;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PingPong.Tests;

public sealed class StandingsIntegrationTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public StandingsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task After_Submitting_Match_Standings_Includes_Both_Players()
    {
        // Arrange
        var client = _factory.CreateClient();
        var matchDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var request = new MatchSubmissionDto(
            "Alice",
            "Bob",
            matchDate,
            new List<SetScoreDto>
            {
                new(11, 8),
                new(11, 9)
            },
            "standings-integration");

        // Act: submit a match
        var response = await client.PostAsJsonAsync("/matches", request);
        response.EnsureSuccessStatusCode();

        // Assert via API: `/api/standings` returns both players with 1 match
        var standingsResponse = await client.GetAsync("/api/standings");
        standingsResponse.EnsureSuccessStatusCode();

        var json = await standingsResponse.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<StandingsPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, r => r.PlayerName == "Alice" && r.MatchesPlayed == 1);
        Assert.Contains(payload!.Items, r => r.PlayerName == "Bob" && r.MatchesPlayed == 1);
    }

    private sealed class StandingsPayload
    {
        [JsonPropertyName("items")]
        public List<StandingRowDto> Items { get; set; } = new();
    }

    private sealed class StandingRowDto
    {
        [JsonPropertyName("playerId")] public Guid PlayerId { get; set; }
        [JsonPropertyName("playerName")] public string PlayerName { get; set; } = string.Empty;
        [JsonPropertyName("matchesPlayed")] public int MatchesPlayed { get; set; }
        [JsonPropertyName("wins")] public int Wins { get; set; }
        [JsonPropertyName("losses")] public int Losses { get; set; }
        [JsonPropertyName("winPercentage")] public double WinPercentage { get; set; }
        [JsonPropertyName("currentRating")] public double CurrentRating { get; set; }
    }
}
