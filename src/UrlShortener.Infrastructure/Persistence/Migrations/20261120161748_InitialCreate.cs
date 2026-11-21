using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrlShortener.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261120161748_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "short_links",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                original_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                click_count = table.Column<long>(type: "bigint", nullable: false),
                last_accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_short_links", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "click_events",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                link_id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                referrer = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_click_events", x => x.id);
                table.ForeignKey(
                    name: "FK_click_events_short_links_link_id",
                    column: x => x.link_id,
                    principalTable: "short_links",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_click_events_link_id_occurred_at",
            table: "click_events",
            columns: new[] { "link_id", "occurred_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_click_events_occurred_at",
            table: "click_events",
            column: "occurred_at");

        migrationBuilder.CreateIndex(
            name: "ix_short_links_active_expires",
            table: "short_links",
            columns: new[] { "is_active", "expires_at" });

        migrationBuilder.CreateIndex(
            name: "ux_short_links_code",
            table: "short_links",
            column: "code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "click_events");

        migrationBuilder.DropTable(
            name: "short_links");
    }
}
