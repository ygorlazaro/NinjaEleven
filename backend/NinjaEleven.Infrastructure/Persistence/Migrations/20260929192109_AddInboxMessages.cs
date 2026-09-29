using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sender_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    mentions = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    link_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    link_route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_inbox_messages_teams_recipient_team_id",
                        column: x => x.recipient_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_recipient_team_id_created_at",
                table: "inbox_messages",
                columns: new[] { "recipient_team_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_recipient_team_id_is_read",
                table: "inbox_messages",
                columns: new[] { "recipient_team_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_recipient_team_id_reference",
                table: "inbox_messages",
                columns: new[] { "recipient_team_id", "reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");
        }
    }
}
