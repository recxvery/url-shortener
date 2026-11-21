using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Persistence.Configurations;

public sealed class ClickEventConfiguration : IEntityTypeConfiguration<ClickEvent>
{
    public void Configure(EntityTypeBuilder<ClickEvent> builder)
    {
        builder.ToTable("click_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.LinkId)
            .HasColumnName("link_id")
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(512);

        builder.Property(x => x.Referrer)
            .HasColumnName("referrer")
            .HasMaxLength(2048);

        builder.HasOne<ShortLink>()
            .WithMany()
            .HasForeignKey(x => x.LinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.LinkId, x.OccurredAt })
            .HasDatabaseName("ix_click_events_link_id_occurred_at")
            .IsDescending(false, true);

        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("ix_click_events_occurred_at");
    }
}
