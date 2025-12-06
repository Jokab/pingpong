using Microsoft.EntityFrameworkCore;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence;

public sealed class PingPongDbContext(DbContextOptions<PingPongDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerAlias> PlayerAliases => Set<PlayerAlias>();

    public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<MatchSet> MatchSets => Set<MatchSet>();

    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();

    public DbSet<MatchEventSetEntity> MatchEventSets => Set<MatchEventSetEntity>();

    public DbSet<Tournament> Tournaments => Set<Tournament>();

    public DbSet<TournamentParticipant> TournamentParticipants => Set<TournamentParticipant>();

    public DbSet<TournamentFixture> TournamentFixtures => Set<TournamentFixture>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PingPongDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Ensure proper PostgreSQL types for common .NET types
        configurationBuilder.Properties<Guid>().HaveColumnType("uuid");
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp");
        configurationBuilder.Properties<decimal>().HaveColumnType("numeric");
    }
}
