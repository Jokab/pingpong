using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;
using PingPong.Domain.MatchSubmission;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class MatchEventSetConfiguration : IEntityTypeConfiguration<MatchEventSetEntity>
{
    public void Configure(EntityTypeBuilder<MatchEventSetEntity> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SetNumber).IsRequired();

        builder.Property(s => s.PlayerOneScore).IsRequired(false);

        builder.Property(s => s.PlayerTwoScore).IsRequired(false);

        builder.Property(s => s.PlayerOneWon).IsRequired(false);

        builder.HasIndex(s => new { s.MatchEventId, s.SetNumber }).IsUnique();

        builder
            .HasOne(s => s.MatchEvent)
            .WithMany(e => e.Sets)
            .HasForeignKey(s => s.MatchEventId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
