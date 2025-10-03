using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class MatchEventSetConfiguration : IEntityTypeConfiguration<MatchEventSet>
{
    public void Configure(EntityTypeBuilder<MatchEventSet> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SetNumber)
            .IsRequired();

        builder.HasIndex(s => new { s.MatchEventId, s.SetNumber })
            .IsUnique();

        builder.HasOne(s => s.MatchEvent)
            .WithMany(e => e.Sets)
            .HasForeignKey(s => s.MatchEventId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
