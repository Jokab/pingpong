using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Tournaments;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();

        builder.Property(t => t.Description).HasMaxLength(1000);

        builder.Property(t => t.DurationDays).IsRequired();

        builder.Property(t => t.PointsPerWin).HasDefaultValue(1);

        builder.Property(t => t.Status).HasConversion<int>().IsRequired();

        builder.Property(t => t.CreatedAt).HasPrecision(3).IsRequired();

        builder.Property(t => t.StartedAt).HasPrecision(3);

        builder.Property(t => t.EndsAt).HasPrecision(3);

        builder.Property(t => t.CompletedAt).HasPrecision(3);

        builder
            .HasMany(t => t.Participants)
            .WithOne(p => p.Tournament!)
            .HasForeignKey(p => p.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(t => t.Fixtures)
            .WithOne(f => f.Tournament!)
            .HasForeignKey(f => f.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.Status);
    }
}
