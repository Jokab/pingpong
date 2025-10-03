using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class PlayerRatingConfiguration : IEntityTypeConfiguration<PlayerRating>
{
    public void Configure(EntityTypeBuilder<PlayerRating> builder)
    {
        builder.HasKey(r => r.PlayerId);

        builder.Property(r => r.CurrentRating)
            .HasPrecision(8, 2);

        builder.Property(r => r.LastUpdatedAt)
            .HasPrecision(3);
    }
}
