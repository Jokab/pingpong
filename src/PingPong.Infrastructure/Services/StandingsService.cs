using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.Entities;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class StandingsService : IStandingsService
{
    private readonly PingPongDbContext _context;

    public StandingsService(PingPongDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StandingRow>> GetStandingsAsync(CancellationToken cancellationToken = default)
    {
        var players = await _context.Players
            .AsNoTracking()
            .Select(player => new PlayerProjection(
                player.Id,
                player.DisplayName,
                player.Rating != null ? player.Rating.CurrentRating : 0d))
            .ToListAsync(cancellationToken);

        if (players.Count == 0)
        {
            return Array.Empty<StandingRow>();
        }

        var playerStats = players.ToDictionary(
            player => player.Id,
            _ => new MutablePlayerStats());

        var matches = await _context.Matches
            .AsNoTracking()
            .Where(match => match.Status == MatchStatus.Active)
            .Select(match => new MatchProjection(
                match.PlayerOneId,
                match.PlayerTwoId,
                match.PlayerOneSetsWon,
                match.PlayerTwoSetsWon))
            .ToListAsync(cancellationToken);

        foreach (var match in matches)
        {
            if (!playerStats.TryGetValue(match.PlayerOneId, out var playerOneStats))
            {
                playerOneStats = new MutablePlayerStats();
                playerStats[match.PlayerOneId] = playerOneStats;
            }

            if (!playerStats.TryGetValue(match.PlayerTwoId, out var playerTwoStats))
            {
                playerTwoStats = new MutablePlayerStats();
                playerStats[match.PlayerTwoId] = playerTwoStats;
            }

            if (match.PlayerOneSetsWon == match.PlayerTwoSetsWon)
            {
                continue;
            }

            playerOneStats.MatchesPlayed++;
            playerTwoStats.MatchesPlayed++;

            if (match.PlayerOneSetsWon > match.PlayerTwoSetsWon)
            {
                playerOneStats.Wins++;
                playerTwoStats.Losses++;
            }
            else
            {
                playerTwoStats.Wins++;
                playerOneStats.Losses++;
            }
        }

        var standings = players
            .Select(player =>
            {
                var stats = playerStats[player.Id];
                var matchesPlayed = stats.MatchesPlayed;
                var winPercentage = matchesPlayed == 0
                    ? 0d
                    : Math.Round((double)stats.Wins / matchesPlayed, 4, MidpointRounding.AwayFromZero);

                return new StandingRow(
                    player.Id,
                    player.DisplayName,
                    matchesPlayed,
                    stats.Wins,
                    stats.Losses,
                    winPercentage,
                    player.Rating);
            })
            .OrderByDescending(row => row.WinPercentage)
            .ThenByDescending(row => row.Wins)
            .ThenByDescending(row => row.MatchesPlayed)
            .ThenByDescending(row => row.CurrentRating)
            .ThenBy(row => row.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return standings;
    }

    private sealed record PlayerProjection(Guid Id, string DisplayName, double Rating);

    private sealed record MatchProjection(Guid PlayerOneId, Guid PlayerTwoId, int PlayerOneSetsWon, int PlayerTwoSetsWon);

    private sealed class MutablePlayerStats
    {
        public int MatchesPlayed { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }
    }
}
