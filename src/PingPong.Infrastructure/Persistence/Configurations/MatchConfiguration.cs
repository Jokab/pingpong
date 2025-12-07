using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.MatchSubmission;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.HasKey(m => m.Id);

        builder
            .Property(m => m.MatchDate)
            .HasConversion(
                value => value.ToDateTime(TimeOnly.MinValue),
                value => DateOnly.FromDateTime(value)
            )
            .HasColumnType("date");

        builder.Property(m => m.CreatedAt).HasPrecision(3);

        builder.Property(m => m.UpdatedAt).HasPrecision(3);

        builder
            .HasOne(m => m.PlayerOne)
            .WithMany()
            .HasForeignKey(m => m.PlayerOneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(m => m.PlayerTwo)
            .WithMany()
            .HasForeignKey(m => m.PlayerTwoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(m => m.PrimaryEvent)
            .WithOne()
            .HasForeignKey<Match>(m => m.PrimaryEventId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder
            .HasMany(m => m.Events)
            .WithOne(e => e.Match)
            .HasForeignKey(e => e.MatchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
