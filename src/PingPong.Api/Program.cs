using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PingPong.Api.Components;
using PingPong.Api.Contracts;
using PingPong.Api.Services;
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
builder.Services.AddScoped<UserIdentityService>();
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
        if (string.IsNullOrWhiteSpace(dto.PlayerOneName) || string.IsNullOrWhiteSpace(dto.PlayerTwoName))
        {
            return Results.BadRequest(new { error = "Both player names are required." });
        }

        var matchDate = dto.MatchDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        List<SetScore> sets;
        List<SetWinner> outcomeOnlySets;
        bool? playerOneWon = null;
        switch (dto)
        {
            case ScoredMatchSubmissionDto scored:
                sets = scored.Sets.Select((set, index) => new SetScore(index + 1, set.PlayerOneScore, set.PlayerTwoScore)).ToList();
                outcomeOnlySets = [];
                break;
            case OutcomeOnlyMatchSubmissionDto outcome:
                sets = [];
                outcomeOnlySets = (outcome.Sets ?? [])
                    .Select(s => new SetWinner(s.SetNumber, s.PlayerOneWon))
                    .ToList();
                playerOneWon = outcome.PlayerOneWon;
                break;
            default:
                return Results.BadRequest(new { error = "Unknown submission kind." });
        }

        if (sets.Count == 0 && outcomeOnlySets.Count == 0 && playerOneWon is null)
        {
            return Results.BadRequest(new { error = "Provide either sets, outcome-only set winners, or PlayerOneWon." });
        }

        var request = new MatchSubmissionRequest(
            dto.PlayerOneName,
            dto.PlayerTwoName,
            matchDate,
            sets,
            outcomeOnlySets,
            playerOneWon,
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
        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            return Results.BadRequest(new { error = "displayName is required" });
        var player = await directory.AddPlayerAsync(dto.DisplayName, ct);
        return Results.Created($"/api/players/{player.Id}", new { id = player.Id, displayName = player.DisplayName });
    })
    .WithName("CreatePlayer");
    //.DisableAntiforgery(); // Alternative: disable antiforgery on this endpoint.

app.MapGet("/api/suggestions/opponents", async (
        string? me,
        int? take,
        PingPong.Infrastructure.Persistence.PingPongDbContext db,
        CancellationToken ct) =>
    {
        var top = take.GetValueOrDefault(5);
        top = top is <= 0 or > 20 ? 5 : top;

        Guid? meId = null;
        if (!string.IsNullOrWhiteSpace(me))
        {
            var name = me.Trim();
            meId = await db.Players
                .AsNoTracking()
                .Where(p => p.DisplayName.ToLower() == name.ToLower())
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        var suggestions = new List<(Guid id, string name, int count)>();
        if (meId is not null)
        {
            var id = meId.Value;
            suggestions = await db.MatchEvents
                .AsNoTracking()
                .Where(e => e.PlayerOneId == id || e.PlayerTwoId == id)
                .Select(e => e.PlayerOneId == id ? e.PlayerTwoId : e.PlayerOneId)
                .GroupBy(x => x)
                .Select(g => new { OpponentId = g.Key, C = g.Count() })
                .OrderByDescending(x => x.C)
                .ThenBy(x => x.OpponentId)
                .Take(top)
                .Join(db.Players.AsNoTracking(), x => x.OpponentId, p => p.Id, (x, p) => new { p.Id, p.DisplayName, x.C })
                .Select(x => new { x.Id, x.DisplayName, x.C })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(r => (r.Id, r.DisplayName, r.C)).ToList(), ct);
        }

        if (suggestions.Count < top)
        {
            var exclude = meId.HasValue ? new HashSet<Guid> { meId.Value } : new HashSet<Guid>();
            var fill = await db.MatchEvents
                .AsNoTracking()
                .SelectMany(e => new[] { e.PlayerOneId, e.PlayerTwoId })
                .Where(pid => !exclude.Contains(pid))
                .GroupBy(pid => pid)
                .Select(g => new { PlayerId = g.Key, C = g.Count() })
                .OrderByDescending(x => x.C)
                .ThenBy(x => x.PlayerId)
                .Take(top - suggestions.Count)
                .Join(db.Players.AsNoTracking(), x => x.PlayerId, p => p.Id, (x, p) => new { p.Id, p.DisplayName, x.C })
                .Select(x => new { x.Id, x.DisplayName, x.C })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(r => (r.Id, r.DisplayName, r.C)).ToList(), ct);

            suggestions.AddRange(fill);
        }

        return Results.Ok(new { items = suggestions.Select(s => new {
            s.id, displayName = s.name,
            s.count }).ToList() });
    })
    .WithName("SuggestOpponents");

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
