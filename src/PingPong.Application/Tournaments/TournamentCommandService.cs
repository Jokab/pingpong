using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Application.Players;
using PingPong.Application.Shared;
using PingPong.Application.Tournaments;
using PingPong.Domain.Players;
using PingPong.Domain.Tournaments;

namespace PingPong.Application.Tournaments;

public sealed class TournamentCommandService : ITournamentCommandService
{
    private readonly IPingPongDbContext _context;
    private readonly IPlayerDirectory _playerDirectory;

    public TournamentCommandService(IPingPongDbContext context, IPlayerDirectory playerDirectory)
    {
        _context = context;
        _playerDirectory = playerDirectory;
    }

    public async Task<TournamentSummary> CreateTournamentAsync(
        CreateTournamentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Tournament name is required.", nameof(request));
        }

        var durationDays = Math.Max(1, request.DurationDays);
        var now = DateTimeOffset.UtcNow;

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description!.Trim(),
            DurationDays = durationDays,
            PointsPerWin = request.PointsPerWin <= 0 ? 1 : request.PointsPerWin,
            Status = TournamentStatus.Draft,
            CreatedAt = now,
        };

        await _context.Tournaments.AddAsync(tournament, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityNames = string.Join(", ", ex.Entries.Select(e => e.Metadata.Name));
            throw new InvalidOperationException(
                $"Concurrency conflict when saving tournament join for entities: {entityNames}",
                ex
            );
        }

        return ToSummary(tournament, 0);
    }

    public async Task<TournamentSummary> StartTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default
    )
    {
        var tournament = await _context
            .Tournaments.Include(t => t.Participants)
                .ThenInclude(p => p.Player)
            .Include(t => t.Fixtures)
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            throw new InvalidOperationException("Tournament not found.");
        }

        if (tournament.Status != TournamentStatus.Draft)
        {
            throw new InvalidOperationException("Tournament has already been started.");
        }

        if (tournament.Participants.Count < 2)
        {
            throw new InvalidOperationException("At least two participants are required to start.");
        }

        List<TournamentFixture>? newlyGeneratedFixtures = null;
        if (tournament.Fixtures.Count == 0)
        {
            newlyGeneratedFixtures = GenerateFixtures(tournament);
            await _context.TournamentFixtures.AddRangeAsync(
                newlyGeneratedFixtures,
                cancellationToken
            );
        }

        var now = DateTimeOffset.UtcNow;
        tournament.Status = TournamentStatus.Active;
        tournament.StartedAt = now;
        tournament.EndsAt = now.AddDays(tournament.DurationDays);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityNames = string.Join(", ", ex.Entries.Select(e => e.Metadata.Name));
            throw new InvalidOperationException(
                $"Concurrency conflict when starting tournament for entities: {entityNames}",
                ex
            );
        }

        return ToSummary(tournament, tournament.Participants.Count);
    }

    public async Task<TournamentStandingRow> JoinTournamentAsync(
        Guid tournamentId,
        string playerName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new ArgumentException("Player name is required.", nameof(playerName));
        }

        var tournament = await _context
            .Tournaments.Include(t => t.Participants)
                .ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            throw new InvalidOperationException("Tournament not found.");
        }

        if (tournament.Status != TournamentStatus.Draft)
        {
            throw new InvalidOperationException("Tournament is closed for new participants.");
        }

        var player = await _playerDirectory.EnsurePlayerAsync(playerName, cancellationToken);

        if (tournament.Participants.Any(p => p.PlayerId == player.Id))
        {
            return ToStandingRow(
                tournament.Participants.First(p => p.PlayerId == player.Id),
                player
            );
        }

        var participant = new TournamentParticipant
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            Tournament = tournament,
            PlayerId = player.Id,
            Player = player,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        await _context.TournamentParticipants.AddAsync(participant, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityNames = string.Join(", ", ex.Entries.Select(e => e.Metadata.Name));
            throw new InvalidOperationException(
                $"Concurrency conflict when saving tournament join for entities: {entityNames}",
                ex
            );
        }

        return ToStandingRow(participant, player);
    }

    public async Task LeaveTournamentAsync(
        Guid tournamentId,
        string playerName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new ArgumentException("Player name is required.", nameof(playerName));
        }

        var normalized = Player.NormalizeKey(playerName);

        var tournament = await _context
            .Tournaments.Include(t => t.Participants)
                .ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            return;
        }

        if (tournament.Status != TournamentStatus.Draft)
        {
            throw new InvalidOperationException(
                "Participants can only leave before the tournament starts."
            );
        }

        var participant = tournament.Participants.FirstOrDefault(p =>
            p.Player != null && p.Player.NormalizedName == normalized
        );

        if (participant is null)
        {
            return;
        }

        _context.TournamentParticipants.Remove(participant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFixtureResultAsync(
        Guid fixtureId,
        Guid winnerPlayerId,
        Guid matchEventId,
        CancellationToken cancellationToken = default
    )
    {
        var fixture = await _context
            .TournamentFixtures.Include(f => f.Tournament)
                .ThenInclude(t => t!.Participants)
            .FirstOrDefaultAsync(f => f.Id == fixtureId, cancellationToken);

        if (fixture is null)
        {
            throw new InvalidOperationException("Fixture not found.");
        }

        if (fixture.Status == TournamentFixtureStatus.Completed)
        {
            return;
        }

        if (winnerPlayerId != fixture.PlayerOneId && winnerPlayerId != fixture.PlayerTwoId)
        {
            throw new InvalidOperationException("Winner is not part of the fixture.");
        }

        if (fixture.MatchEventId is not null)
        {
            throw new InvalidOperationException("Fixture already linked to a match event.");
        }

        var tournament =
            fixture.Tournament
            ?? throw new InvalidOperationException("Tournament missing for fixture.");
        var winner = tournament.Participants.FirstOrDefault(p => p.PlayerId == winnerPlayerId);
        var loserId =
            winnerPlayerId == fixture.PlayerOneId ? fixture.PlayerTwoId : fixture.PlayerOneId;
        var loser = tournament.Participants.FirstOrDefault(p => p.PlayerId == loserId);

        if (winner is null || loser is null)
        {
            throw new InvalidOperationException("Participants for fixture could not be resolved.");
        }

        fixture.Status = TournamentFixtureStatus.Completed;
        fixture.MatchEventId = matchEventId;
        fixture.WinnerPlayerId = winnerPlayerId;
        fixture.CompletedAt = DateTimeOffset.UtcNow;

        winner.MatchesPlayed += 1;
        winner.Wins += 1;
        winner.Points += tournament.PointsPerWin;

        loser.MatchesPlayed += 1;
        loser.Losses += 1;

        await _context.SaveChangesAsync(cancellationToken);

        var remaining = await _context
            .TournamentFixtures.AsNoTracking()
            .Where(f =>
                f.TournamentId == tournament.Id && f.Status == TournamentFixtureStatus.Pending
            )
            .AnyAsync(cancellationToken);

        if (!remaining)
        {
            tournament.Status = TournamentStatus.Completed;
            tournament.CompletedAt = DateTimeOffset.UtcNow;

            if (tournament.EndsAt is null)
            {
                tournament.EndsAt = tournament.CompletedAt;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static TournamentSummary ToSummary(Tournament tournament, int participantCount)
    {
        return new TournamentSummary(
            tournament.Id,
            tournament.Name,
            tournament.Description,
            tournament.Status,
            tournament.DurationDays,
            participantCount,
            tournament.CreatedAt,
            tournament.StartedAt,
            tournament.EndsAt
        );
    }

    private static TournamentStandingRow ToStandingRow(
        TournamentParticipant participant,
        Player player
    )
    {
        var rating = player.Rating?.CurrentRating ?? 0d;
        return new TournamentStandingRow(
            participant.PlayerId,
            player.DisplayName,
            participant.MatchesPlayed,
            participant.Wins,
            participant.Losses,
            participant.Points,
            rating
        );
    }

    private static List<TournamentFixture> GenerateFixtures(Tournament tournament)
    {
        var orderedParticipants = tournament
            .Participants.OrderBy(
                p => p.Player?.DisplayName ?? string.Empty,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();

        var fixtures = new List<TournamentFixture>();
        var sequence = 1;

        for (var i = 0; i < orderedParticipants.Count - 1; i++)
        {
            for (var j = i + 1; j < orderedParticipants.Count; j++)
            {
                var first = orderedParticipants[i];
                var second = orderedParticipants[j];

                fixtures.Add(
                    new TournamentFixture
                    {
                        Id = Guid.NewGuid(),
                        TournamentId = tournament.Id,
                        PlayerOneId = first.PlayerId,
                        PlayerTwoId = second.PlayerId,
                        Status = TournamentFixtureStatus.Pending,
                        CreatedAt = DateTimeOffset.UtcNow,
                        RoundNumber = sequence,
                        Sequence = sequence,
                    }
                );

                sequence++;
            }
        }

        return fixtures;
    }
}
