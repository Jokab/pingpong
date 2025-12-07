using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class TournamentFixtureConfiguration : IEntityTypeConfiguration<TournamentFixture>
{
    public void Configure(EntityTypeBuilder<TournamentFixture> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Status).HasConversion<int>().IsRequired();

        builder.Property(f => f.CreatedAt).HasPrecision(3).IsRequired();

        builder.Property(f => f.CompletedAt).HasPrecision(3);

        builder.Property(f => f.RoundNumber).IsRequired();

        builder.Property(f => f.Sequence).IsRequired();

        builder
            .HasIndex(f => new
            {
                f.TournamentId,
                f.PlayerOneId,
                f.PlayerTwoId,
            })
            .IsUnique();

        builder.HasIndex(f => new { f.TournamentId, f.Status });

        builder
            .HasOne(f => f.PlayerOne)
            .WithMany()
            .HasForeignKey(f => f.PlayerOneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(f => f.PlayerTwo)
            .WithMany()
            .HasForeignKey(f => f.PlayerTwoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(f => f.MatchEvent)
            .WithMany()
            .HasForeignKey(f => f.MatchEventId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
