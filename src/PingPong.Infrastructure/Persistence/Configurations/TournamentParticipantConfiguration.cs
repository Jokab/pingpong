using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class TournamentParticipantConfiguration
    : IEntityTypeConfiguration<TournamentParticipant>
{
    public void Configure(EntityTypeBuilder<TournamentParticipant> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.JoinedAt).HasPrecision(3).IsRequired();

        builder.Property(p => p.Points).HasDefaultValue(0);

        builder.Property(p => p.MatchesPlayed).HasDefaultValue(0);

        builder.Property(p => p.Wins).HasDefaultValue(0);

        builder.Property(p => p.Losses).HasDefaultValue(0);

        builder.HasIndex(p => new { p.TournamentId, p.PlayerId }).IsUnique();

        builder
            .HasOne(p => p.Player)
            .WithMany()
            .HasForeignKey(p => p.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
