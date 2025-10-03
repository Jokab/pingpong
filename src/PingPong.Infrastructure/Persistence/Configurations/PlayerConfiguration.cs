using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PingPong.Domain.Entities;

namespace PingPong.Infrastructure.Persistence.Configurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.NormalizedName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(p => p.NormalizedName)
            .IsUnique();

        builder.Property(p => p.CreatedAt)
            .HasPrecision(3);

        builder.HasOne(p => p.Rating)
            .WithOne(r => r.Player)
            .HasForeignKey<PlayerRating>(r => r.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
