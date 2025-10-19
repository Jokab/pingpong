using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using PingPong.Api.Components;
using PingPong.Api.Contracts;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.Exceptions;
using PingPong.Infrastructure.DependencyInjection;
using PingPong.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMudServices();
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});

var app = builder.Build();

// Seed command: supports --seed-data [--seed <int>] [--reseed]
if (args.Contains("--seed-data", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
    var db = scope.ServiceProvider.GetRequiredService<PingPong.Infrastructure.Persistence.PingPongDbContext>();
    db.Database.Migrate();

    int? seedValue = null;
    var reseed = false;
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--seed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed))
        {
            seedValue = parsed;
            i++;
        }
        else if (string.Equals(args[i], "--reseed", StringComparison.OrdinalIgnoreCase))
        {
            reseed = true;
        }
    }

    await seeder.SeedAsync(seedValue, reseed);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAntiforgery();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PingPong.Infrastructure.Persistence.PingPongDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    if (env.IsEnvironment("Testing"))
    {
        // For SQLite in tests
        db.Database.EnsureCreated();
    }
    else
    {
        // For PostgreSQL in dev/prod
        db.Database.Migrate();
    }
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/matches", async (MatchSubmissionDto dto, IMatchSubmissionService matchService, CancellationToken cancellationToken) =>
    {
        if (dto is null)
        {
            return Results.BadRequest(new { error = "Payload is required." });
        }

        if (string.IsNullOrWhiteSpace(dto.PlayerOneName) || string.IsNullOrWhiteSpace(dto.PlayerTwoName))
        {
            return Results.BadRequest(new { error = "Both player names are required." });
        }

        var matchDate = dto.MatchDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var setDtos = dto.Sets ?? Array.Empty<SetScoreDto>();
        var sets = setDtos
            .Select((set, index) => new SetScore(index + 1, set.PlayerOneScore, set.PlayerTwoScore))
            .ToList();

        var request = new MatchSubmissionRequest(
            dto.PlayerOneName,
            dto.PlayerTwoName,
            matchDate,
            sets,
            dto.SubmittedBy);

        try
        {
            var result = await matchService.SubmitMatchAsync(request, cancellationToken);
            return Results.Created($"/matches/{result.MatchId}", new MatchSubmissionResponse(result.MatchId, result.EventId));
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("SubmitMatch")
    .DisableAntiforgery();

app.MapGet("/api/history", async (
        int? page,
        int? pageSize,
        Guid? playerId,
        IHistoryService historyService,
        CancellationToken cancellationToken) =>
    {
        var p = page.GetValueOrDefault(1);
        var ps = pageSize.GetValueOrDefault(25);
        var (items, total) = await historyService.GetHistoryAsync(p, ps, playerId, cancellationToken);
        return Results.Ok(new
        {
            page = p,
            pageSize = ps,
            total,
            items
        });
    })
    .WithName("GetHistory");

app.MapGet("/api/players", async (string? query, int? size, IPlayerDirectory directory, CancellationToken ct) =>
    {
        var q = query ?? string.Empty;
        var s = size.GetValueOrDefault(10);
        var results = await directory.SearchAsync(q, s, ct);
        return Results.Ok(results.Select(r => new { id = r.PlayerId, displayName = r.DisplayName, score = r.Confidence, isExact = r.IsExactMatch }));
    })
    .WithName("SearchPlayers");

app.MapPost("/api/players", async (PlayerCreateDto dto, IPlayerDirectory directory, CancellationToken ct) =>
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.DisplayName))
            return Results.BadRequest(new { error = "displayName is required" });
        var player = await directory.AddPlayerAsync(dto.DisplayName, ct);
        return Results.Created($"/api/players/{player.Id}", new { id = player.Id, displayName = player.DisplayName });
    })
    .WithName("CreatePlayer");
    //.DisableAntiforgery(); // Alternative: disable antiforgery on this endpoint.

app.MapGet("/api/standings", async (IStandingsService standingsService, CancellationToken ct) =>
    {
        var rows = await standingsService.GetStandingsAsync(ct);
        return Results.Ok(new
        {
            items = rows.Select(r => new
            {
                playerId = r.PlayerId,
                playerName = r.PlayerName,
                matchesPlayed = r.MatchesPlayed,
                wins = r.Wins,
                losses = r.Losses,
                winPercentage = r.WinPercentage,
                currentRating = r.CurrentRating
            })
        });
    })
    .WithName("GetStandings");

app.Run();

public partial class Program;
