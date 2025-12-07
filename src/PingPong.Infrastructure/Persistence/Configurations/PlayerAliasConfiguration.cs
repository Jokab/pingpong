using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Players;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class PlayerAliasConfiguration : IEntityTypeConfiguration<PlayerAlias>
{
    public void Configure(EntityTypeBuilder<PlayerAlias> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AliasName).HasMaxLength(200).IsRequired();

        builder.HasIndex(a => new { a.PlayerId, a.AliasName }).IsUnique();

        builder.Property(a => a.CreatedAt).HasPrecision(3);

        builder.Property(a => a.ConfidenceScore).HasPrecision(5, 4);
    }
}
