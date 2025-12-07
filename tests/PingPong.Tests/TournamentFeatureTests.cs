using System.Net;
using System.Net.Http.Json;
using PingPong.Api.Contracts;
using PingPong.Api.Contracts.MatchSubmission;
using PingPong.Domain.Entities;
using PingPong.Tests.Support;

namespace PingPong.Tests;

public sealed class TournamentFeatureTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public TournamentFeatureTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StartingTournamentGeneratesRoundRobinFixtures()
    {
        var client = _factory.CreateClient();
        var summary = await CreateTournamentAsync(client);

        var players = new[] { UniquePlayerName("A"), UniquePlayerName("B"), UniquePlayerName("C") };

        foreach (var player in players)
        {
            await JoinTournamentAsync(client, summary.Id, player);
        }

        await StartTournamentAsync(client, summary.Id);

        var details = await GetTournamentDetailsAsync(client, summary.Id);
        var summaryDetails = details.Summary;
        Assert.NotNull(summaryDetails);
        Assert.Equal(TournamentStatus.Active, summaryDetails!.StatusValue);
        Assert.Equal(3, details.Fixtures.Count);

        var uniquePairs = details
            .Fixtures.Select(f => NormalizePair(f.PlayerOneName, f.PlayerTwoName))
            .ToHashSet();

        Assert.Equal(3, uniquePairs.Count);
    }

    [Fact]
    public async Task JoiningAfterStartIsRejected()
    {
        var client = _factory.CreateClient();
        var summary = await CreateTournamentAsync(client);

        await JoinTournamentAsync(client, summary.Id, UniquePlayerName("Seed1"));
        await JoinTournamentAsync(client, summary.Id, UniquePlayerName("Seed2"));
        await StartTournamentAsync(client, summary.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{summary.Id}/join",
            new TournamentParticipantDto(UniquePlayerName("Late"))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmittingTournamentMatchUpdatesStandings()
    {
        var client = _factory.CreateClient();
        var summary = await CreateTournamentAsync(client);

        var playerOne = UniquePlayerName("Winner");
        var playerTwo = UniquePlayerName("Loser");

        await JoinTournamentAsync(client, summary.Id, playerOne);
        await JoinTournamentAsync(client, summary.Id, playerTwo);
        await StartTournamentAsync(client, summary.Id);

        var details = await GetTournamentDetailsAsync(client, summary.Id);
        var fixture = Assert.Single(details.Fixtures);

        MatchSubmissionDto submission = new ScoredMatchSubmissionDto(
            fixture.PlayerOneName,
            fixture.PlayerTwoName,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            new List<SetScoreDto> { new(11, 7), new(11, 5) },
            "turnering-test",
            fixture.Id
        );

        var submissionResponse = await client.PostAsJsonAsync("/matches", submission);
        var submissionBody = await submissionResponse.Content.ReadAsStringAsync();
        Assert.True(submissionResponse.IsSuccessStatusCode, submissionBody);

        var updated = await GetTournamentDetailsAsync(client, summary.Id);
        var winnerRow = updated.Standings.Single(s => s.PlayerId == fixture.PlayerOneId);
        var loserRow = updated.Standings.Single(s => s.PlayerId == fixture.PlayerTwoId);
        var updatedFixture = updated.Fixtures.Single(f => f.Id == fixture.Id);

        Assert.Equal(1, winnerRow.MatchesPlayed);
        Assert.Equal(1, winnerRow.Wins);
        Assert.Equal(1, winnerRow.Points);

        Assert.Equal(1, loserRow.MatchesPlayed);
        Assert.Equal(1, loserRow.Losses);
        Assert.Equal(0, loserRow.Points);

        Assert.Equal(TournamentFixtureStatus.Completed, updatedFixture.StatusValue);
        Assert.Equal(fixture.PlayerOneId, updatedFixture.WinnerPlayerId);
        Assert.NotNull(updatedFixture.MatchEventId);
    }

    private static string UniquePlayerName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string NormalizePair(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    private static string UniqueTournamentName() => $"Turnering-{Guid.NewGuid():N}";

    private async Task<TournamentSummaryResponse> CreateTournamentAsync(HttpClient client)
    {
        var dto = new TournamentCreateDto(UniqueTournamentName(), "Integrationstest", 14);
        var response = await client.PostAsJsonAsync("/api/admin/tournaments", dto);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return (await response.Content.ReadFromJsonAsync<TournamentSummaryResponse>())!;
    }

    private static async Task JoinTournamentAsync(
        HttpClient client,
        Guid tournamentId,
        string playerName
    )
    {
        var response = await client.PostAsJsonAsync(
            $"/api/tournaments/{tournamentId}/join",
            new TournamentParticipantDto(playerName)
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static async Task StartTournamentAsync(HttpClient client, Guid tournamentId)
    {
        using var content = new StringContent(string.Empty);
        var response = await client.PostAsync(
            $"/api/admin/tournaments/{tournamentId}/start",
            content
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static async Task<TournamentDetailsResponse> GetTournamentDetailsAsync(
        HttpClient client,
        Guid tournamentId
    )
    {
        var response = await client.GetAsync($"/api/tournaments/{tournamentId}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return (await response.Content.ReadFromJsonAsync<TournamentDetailsResponse>())!;
    }
}
