using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.MatchSubmission;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CreatedAt).HasPrecision(3);

        builder
            .Property(e => e.MatchDate)
            .HasConversion(
                value => value.ToDateTime(TimeOnly.MinValue),
                value => DateOnly.FromDateTime(value)
            )
            .HasColumnType("date");

        builder
            .HasOne(e => e.PlayerOne)
            .WithMany()
            .HasForeignKey(e => e.PlayerOneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.PlayerTwo)
            .WithMany()
            .HasForeignKey(e => e.PlayerTwoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.SupersededEvent)
            .WithMany()
            .HasForeignKey(e => e.SupersedesEventId)
            .OnDelete(DeleteBehavior.Restrict);

        // In the event-sourced model, MatchId/Match navigation is not used as a required FK
        builder.Property(e => e.MatchId).IsRequired(false);

        builder
            .HasOne(e => e.Match)
            .WithMany(m => m.Events)
            .HasForeignKey(e => e.MatchId)
            .OnDelete(DeleteBehavior.NoAction);

        // TPH mapping for scored vs outcome-only events
        builder
            .HasDiscriminator<string>("EventKind")
            .HasValue<ScoredMatchEvent>("Scored")
            .HasValue<OutcomeOnlyMatchEvent>("Outcome");
    }
}
