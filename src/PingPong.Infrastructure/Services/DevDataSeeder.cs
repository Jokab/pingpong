using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.MatchSubmission;
using PingPong.Application.Models;
using PingPong.Application.Tournaments;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class DevDataSeeder
{
    private readonly PingPongDbContext _dbContext;
    private readonly IPlayerDirectory _playerDirectory;
    private readonly IMatchSubmissionService _matchSubmissionService;
    private readonly ITournamentCommandService _tournamentCommandService;

    public DevDataSeeder(
        PingPongDbContext dbContext,
        IPlayerDirectory playerDirectory,
        IMatchSubmissionService matchSubmissionService,
        ITournamentCommandService tournamentCommandService
    )
    {
        _dbContext = dbContext;
        _playerDirectory = playerDirectory;
        _matchSubmissionService = matchSubmissionService;
        _tournamentCommandService = tournamentCommandService;
    }

    public async Task SeedAsync(
        int? seedValue = null,
        bool reseed = false,
        CancellationToken cancellationToken = default
    )
    {
        if (reseed)
        {
            await ClearAsync(cancellationToken);
        }

        // Prepare RNG (deterministic if seed provided)
        var rng = seedValue.HasValue ? new Random(seedValue.Value) : new Random();

        // About 15 Swedish first names
        var swedishFirstNames = new[]
        {
            "Erik",
            "Johan",
            "Lars",
            "Anders",
            "Per",
            "Nils",
            "Karl",
            "Mikael",
            "Magnus",
            "Björn",
            "Oskar",
            "Henrik",
            "Fredrik",
            "Patrik",
            "Jens",
        };

        // Ensure players exist
        var playerInfos = new List<PlayerSeedInfo>(swedishFirstNames.Length);
        foreach (var name in swedishFirstNames)
        {
            var p = await _playerDirectory.EnsurePlayerAsync(name, cancellationToken);
            playerInfos.Add(new PlayerSeedInfo(p.Id, p.DisplayName));
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        var playerIds = playerInfos.Select(info => info.Id).ToList();

        // Define rivalries (some pairs meet more often)
        var rivalPairs = new List<(int a, int b)>
        {
            (0, 1), // Erik vs Johan
            (7, 9), // Mikael vs Björn
            (4, 6), // Per vs Karl
        };

        // Generate matches for approximately the last 12 weeks on weekdays only
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddDays(-7 * 12);

        // Track per-day play counts per player to enforce max two matches/day per player
        var perDayCounts = new Dictionary<DateOnly, Dictionary<Guid, int>>();

        for (var date = start; date <= today; date = date.AddDays(1))
        {
            var dayOfWeek = new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
            if (dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue; // no weekend matches
            }

            // Randomly choose number of matches for the day (0-3, skewed towards 1-2)
            var matchCountRoll = rng.NextDouble();
            var matchesToday =
                matchCountRoll < 0.15 ? 0
                : matchCountRoll < 0.70 ? 1
                : matchCountRoll < 0.95 ? 2
                : 3;

            if (matchesToday == 0)
            {
                continue;
            }

            if (!perDayCounts.TryGetValue(date, out var dayCounts))
            {
                dayCounts = new Dictionary<Guid, int>();
                perDayCounts[date] = dayCounts;
            }

            var scheduled = 0;
            var guard = 0;
            while (scheduled < matchesToday && guard < 50)
            {
                guard++;

                // Weighted selection: 60% chance to pick a rivalry pair, otherwise random distinct players
                int idxA;
                int idxB;
                if (rng.NextDouble() < 0.6)
                {
                    var (a, b) = rivalPairs[rng.Next(rivalPairs.Count)];
                    idxA = a;
                    idxB = b;
                }
                else
                {
                    idxA = rng.Next(playerIds.Count);
                    do
                    {
                        idxB = rng.Next(playerIds.Count);
                    } while (idxB == idxA);
                }

                var p1 = playerIds[idxA];
                var p2 = playerIds[idxB];

                // Enforce max two matches per day per player
                var p1Count = dayCounts.GetValueOrDefault(p1, 0);
                var p2Count = dayCounts.GetValueOrDefault(p2, 0);
                if (p1Count >= 2 || p2Count >= 2)
                {
                    continue;
                }

                // Build a best-of-3 style match (max 3 sets)
                var sets = GenerateBestOfThreeSets(rng);

                var p1Name = playerInfos[idxA].Name;
                var p2Name = playerInfos[idxB].Name;

                var request = new MatchSubmissionRequest(
                    p1Name,
                    p2Name,
                    date,
                    sets,
                    Array.Empty<SetWinner>(),
                    null,
                    "dev-seeder",
                    TournamentFixtureId: null
                );

                await _matchSubmissionService.SubmitMatchAsync(request, cancellationToken);

                dayCounts[p1] = p1Count + 1;
                dayCounts[p2] = p2Count + 1;
                scheduled++;
            }
        }
        await SeedSampleTournamentAsync(playerInfos, rng, cancellationToken);
    }

    private async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // Delete in safe order due to FK constraints:
        // 1) MatchEvents (cascades to MatchEventSets)
        // 2) Matches (if any) and MatchSets
        // 3) Players (cascades to PlayerRatings and PlayerAliases)
        await _dbContext.MatchEvents.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.MatchEventSets.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.MatchSets.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Matches.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Players.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task SeedSampleTournamentAsync(
        IReadOnlyList<PlayerSeedInfo> players,
        Random rng,
        CancellationToken cancellationToken
    )
    {
        if (players.Count < 4)
        {
            return;
        }

        var selected = players.Take(4).ToList();
        var createRequest = new CreateTournamentRequest(
            "Demoturnering",
            "Skapas automatiskt vid utvecklingsseedning.",
            21
        );

        var summary = await _tournamentCommandService.CreateTournamentAsync(
            createRequest,
            cancellationToken
        );

        foreach (var player in selected)
        {
            await _tournamentCommandService.JoinTournamentAsync(
                summary.Id,
                player.Name,
                cancellationToken
            );
        }

        await _tournamentCommandService.StartTournamentAsync(summary.Id, cancellationToken);

        var fixtures = await _dbContext
            .TournamentFixtures.AsNoTracking()
            .Where(f => f.TournamentId == summary.Id)
            .OrderBy(f => f.Sequence)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (fixtures.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        foreach (var fixture in fixtures)
        {
            var playerOneName = players.First(p => p.Id == fixture.PlayerOneId).Name;
            var playerTwoName = players.First(p => p.Id == fixture.PlayerTwoId).Name;

            // Alternate winners to keep standings varied
            var playerOneWins = rng.NextDouble() >= 0.5;
            var sets = GenerateBestOfThreeSets(rng, forcePlayerOneWin: playerOneWins);

            var request = new MatchSubmissionRequest(
                playerOneName,
                playerTwoName,
                today,
                sets,
                Array.Empty<SetWinner>(),
                null,
                "dev-turnering",
                fixture.Id
            );

            await _matchSubmissionService.SubmitMatchAsync(request, cancellationToken);
        }
    }

    private static IReadOnlyList<SetScore> GenerateBestOfThreeSets(
        Random rng,
        bool? forcePlayerOneWin = null
    )
    {
        // Decide if the match goes to 2 or 3 sets. 65% ends 2-0, 35% ends 2-1
        var threeSets = rng.NextDouble() < 0.35;

        // Decide winner (player one or two) with slight bias to be fair
        var p1WinsMatch = forcePlayerOneWin ?? (rng.NextDouble() < 0.5);

        var sets = new List<SetScore>();

        if (threeSets)
        {
            // 2-1 outcome
            var p1WinsFirstTwo = rng.NextDouble() < 0.5;
            if (p1WinsMatch)
            {
                // P1 wins 2 sets; randomize which ones
                if (p1WinsFirstTwo)
                {
                    AddSet(1, true);
                    AddSet(2, true);
                    AddSet(3, false);
                }
                else
                {
                    AddSet(1, true);
                    AddSet(2, false);
                    AddSet(3, true);
                }
            }
            else
            {
                if (p1WinsFirstTwo)
                {
                    AddSet(1, false);
                    AddSet(2, false);
                    AddSet(3, true);
                }
                else
                {
                    AddSet(1, false);
                    AddSet(2, true);
                    AddSet(3, false);
                }
            }
        }
        else
        {
            // 2-0 outcome
            AddSet(1, p1WinsMatch);
            AddSet(2, p1WinsMatch);
        }

        return sets;

        // Helper to generate a single set score: winner gets >=11 and 2-point margin
        static (int w, int l) GenerateSet(Random r)
        {
            var deuce = r.NextDouble() < 0.15; // 15% chance of deuce
            if (deuce)
            {
                var extra = r.Next(0, 6); // 12-10 up to 16-14
                return (11 + extra + 1, 10 + extra);
            }
            else
            {
                var loser = r.Next(3, 10); // 3..9 creates natural-looking scores
                var winner = Math.Max(11, loser + 2);
                if (winner == loser)
                    winner += 2;
                if (winner - loser < 2)
                    winner = loser + 2;
                if (winner < 11)
                    winner = 11;
                return (winner, loser);
            }
        }

        void AddSet(int setNumber, bool p1Wins)
        {
            var (w, l) = GenerateSet(rng);
            sets.Add(p1Wins ? new SetScore(setNumber, w, l) : new SetScore(setNumber, l, w));
        }
    }

    private sealed record PlayerSeedInfo(Guid Id, string Name);
}
