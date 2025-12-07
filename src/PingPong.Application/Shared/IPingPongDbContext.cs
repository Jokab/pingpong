using Microsoft.EntityFrameworkCore;
using PingPong.Domain.MatchSubmission;
using PingPong.Domain.Players;
using PingPong.Domain.Standings;
using PingPong.Domain.Tournaments;

namespace PingPong.Application.Shared;

public interface IPingPongDbContext
{
    DbSet<Player> Players { get; }
    DbSet<PlayerAlias> PlayerAliases { get; }
    DbSet<PlayerRating> PlayerRatings { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchSet> MatchSets { get; }
    DbSet<MatchEvent> MatchEvents { get; }
    DbSet<MatchEventSetEntity> MatchEventSets { get; }
    DbSet<Tournament> Tournaments { get; }
    DbSet<TournamentParticipant> TournamentParticipants { get; }
    DbSet<TournamentFixture> TournamentFixtures { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

