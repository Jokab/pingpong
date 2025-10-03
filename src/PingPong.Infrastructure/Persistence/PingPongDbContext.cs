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

    public DbSet<MatchEventSet> MatchEventSets => Set<MatchEventSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PingPongDbContext).Assembly);
    }
}
