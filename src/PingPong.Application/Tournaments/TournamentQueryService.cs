using Microsoft.EntityFrameworkCore;
using PingPong.Application.Models;
using PingPong.Application.Shared;
using PingPong.Application.Tournaments;
using PingPong.Domain.Entities;
using PingPong.Domain.Tournaments;

namespace PingPong.Application.Tournaments;

public sealed class TournamentQueryService : ITournamentQueryService
{
    private readonly IPingPongDbContext _context;

    public TournamentQueryService(IPingPongDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TournamentSummary>> GetTournamentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Tournaments.AsNoTracking()
            .OrderByDescending(t => t.StartedAt ?? t.CreatedAt)
            .Select(t => new TournamentSummary(
                t.Id,
                t.Name,
                t.Description,
                t.Status,
                t.DurationDays,
                t.Participants.Count,
                t.CreatedAt,
                t.StartedAt,
                t.EndsAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<TournamentDetails?> GetTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default
    )
    {
        var summary = await _context
            .Tournaments.AsNoTracking()
            .Where(t => t.Id == tournamentId)
            .Select(t => new TournamentSummary(
                t.Id,
                t.Name,
                t.Description,
                t.Status,
                t.DurationDays,
                t.Participants.Count,
                t.CreatedAt,
                t.StartedAt,
                t.EndsAt
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return null;
        }

        var standings = await _context
            .TournamentParticipants.AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .Select(p => new TournamentStandingRow(
                p.PlayerId,
                p.Player!.DisplayName,
                p.MatchesPlayed,
                p.Wins,
                p.Losses,
                p.Points,
                p.Player.Rating != null ? p.Player.Rating.CurrentRating : 0d
            ))
            .ToListAsync(cancellationToken);

        standings = standings
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.Wins)
            .ThenByDescending(row => row.MatchesPlayed)
            .ThenByDescending(row => row.CurrentRating)
            .ThenBy(row => row.PlayerName)
            .ToList();

        var fixtures = await GetFixturesAsync(tournamentId, cancellationToken);

        return new TournamentDetails(summary, standings, fixtures);
    }

    public async Task<IReadOnlyList<TournamentFixtureView>> GetFixturesAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TournamentFixtures.AsNoTracking()
            .Where(f => f.TournamentId == tournamentId)
            .OrderBy(f => f.Sequence)
            .Select(f => new TournamentFixtureView(
                f.Id,
                f.TournamentId,
                f.PlayerOneId,
                f.PlayerOne!.DisplayName,
                f.PlayerTwoId,
                f.PlayerTwo!.DisplayName,
                f.Status,
                f.WinnerPlayerId,
                f.MatchEventId,
                f.RoundNumber,
                f.Sequence
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OpenFixtureOption>> GetOpenFixturesAsync(
        string playerOneName,
        string playerTwoName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(playerOneName) || string.IsNullOrWhiteSpace(playerTwoName))
        {
            return Array.Empty<OpenFixtureOption>();
        }

        var normalizedOne = Player.NormalizeKey(playerOneName);
        var normalizedTwo = Player.NormalizeKey(playerTwoName);

        var players = await _context
            .Players.AsNoTracking()
            .Where(p => p.NormalizedName == normalizedOne || p.NormalizedName == normalizedTwo)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.NormalizedName,
            })
            .ToListAsync(cancellationToken);

        var p1 = players.FirstOrDefault(p => p.NormalizedName == normalizedOne);
        var p2 = players.FirstOrDefault(p => p.NormalizedName == normalizedTwo);

        if (p1 is null || p2 is null)
        {
            return Array.Empty<OpenFixtureOption>();
        }

        var fixtures = await _context
            .TournamentFixtures.AsNoTracking()
            .Where(f =>
                f.Status == TournamentFixtureStatus.Pending
                && (
                    (f.PlayerOneId == p1.Id && f.PlayerTwoId == p2.Id)
                    || (f.PlayerOneId == p2.Id && f.PlayerTwoId == p1.Id)
                )
            )
            .Select(f => new
            {
                f.Id,
                f.TournamentId,
                TournamentName = f.Tournament!.Name,
                f.PlayerOneId,
                PlayerOneName = f.PlayerOne!.DisplayName,
                f.PlayerTwoId,
                PlayerTwoName = f.PlayerTwo!.DisplayName,
            })
            .OrderBy(x => x.TournamentName)
            .ToListAsync(cancellationToken);

        return fixtures
            .Select(x =>
            {
                var opponentId = x.PlayerOneId == p1.Id ? x.PlayerTwoId : x.PlayerOneId;
                var opponentName = x.PlayerOneId == p1.Id ? x.PlayerTwoName : x.PlayerOneName;

                return new OpenFixtureOption(
                    x.Id,
                    x.TournamentId,
                    x.TournamentName,
                    x.PlayerOneId,
                    x.PlayerTwoId,
                    opponentId,
                    opponentName
                );
            })
            .ToList();
    }
}
