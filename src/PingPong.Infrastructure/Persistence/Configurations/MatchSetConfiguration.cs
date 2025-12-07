using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.MatchSubmission;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class MatchSetConfiguration : IEntityTypeConfiguration<MatchSet>
{
    public void Configure(EntityTypeBuilder<MatchSet> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SetNumber).IsRequired();

        builder.HasIndex(s => new { s.MatchId, s.SetNumber }).IsUnique();
    }
}
