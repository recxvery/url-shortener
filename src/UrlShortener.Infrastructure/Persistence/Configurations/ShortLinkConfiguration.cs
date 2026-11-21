using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Persistence.Configurations;

public sealed class ShortLinkConfiguration : IEntityTypeConfiguration<ShortLink>
{
    public void Configure(EntityTypeBuilder<ShortLink> builder)
    {
        builder.ToTable("short_links");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.OriginalUrl)
            .HasColumnName("original_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.ClickCount)
            .HasColumnName("click_count")
            .IsRequired();

        builder.Property(x => x.LastAccessedAt)
            .HasColumnName("last_accessed_at");

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_short_links_code");

        builder.HasIndex(x => new { x.IsActive, x.ExpiresAt })
            .HasDatabaseName("ix_short_links_active_expires");
    }
}
