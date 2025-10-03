using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CreatedAt)
            .HasPrecision(3);

        builder.Property(e => e.MatchDate)
            .HasConversion(
                value => value.ToDateTime(TimeOnly.MinValue),
                value => DateOnly.FromDateTime(value))
            .HasColumnType("date");

        builder.HasOne(e => e.PlayerOne)
            .WithMany()
            .HasForeignKey(e => e.PlayerOneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PlayerTwo)
            .WithMany()
            .HasForeignKey(e => e.PlayerTwoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SupersededEvent)
            .WithMany()
            .HasForeignKey(e => e.SupersedesEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
