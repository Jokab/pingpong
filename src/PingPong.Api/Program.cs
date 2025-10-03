using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using MudBlazor.Services;
using PingPong.Api.Components;
using PingPong.Api.Contracts;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.Exceptions;
using PingPong.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMudServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAntiforgery();

// Apply migrations automatically in development to keep local DB up-to-date
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PingPong.Infrastructure.Persistence.PingPongDbContext>();
    // EnsureCreated for now; will move to Migrate after adding Microsoft.EntityFrameworkCore.Design reference at runtime app if needed
    db.Database.EnsureCreated();
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
    .WithName("SubmitMatch");

app.Run();

public partial class Program;
