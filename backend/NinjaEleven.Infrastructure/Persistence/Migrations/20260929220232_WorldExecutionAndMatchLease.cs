using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorldExecutionAndMatchLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_matches_fixture_id",
                table: "matches");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "execution_started_at",
                table: "rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "execution_status",
                table: "rounds",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Scheduled");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "session_heartbeat_at",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "session_host",
                table: "matches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_rounds_execution_status",
                table: "rounds",
                column: "execution_status");

            migrationBuilder.CreateIndex(
                name: "ix_matches_session_host",
                table: "matches",
                column: "session_host");

            migrationBuilder.CreateIndex(
                name: "ux_matches_live_fixture",
                table: "matches",
                column: "fixture_id",
                unique: true,
                filter: "status NOT IN ('Finished', 'Abandoned')");

            // The windows the world has already played. Their claim column is new, so every
            // one of them arrives as "Scheduled" and a Scheduler reading the calendar would
            // offer a matchday that was finished last week to be played again. The window's
            // own completion is what says it, and this writes the two into agreement so a
            // query on either one gives the same answer.
            migrationBuilder.Sql("""
                UPDATE rounds
                   SET execution_status = 'Completed'
                 WHERE completed_at IS NOT NULL
                   AND execution_status = 'Scheduled';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rounds_execution_status",
                table: "rounds");

            migrationBuilder.DropIndex(
                name: "ix_matches_session_host",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ux_matches_live_fixture",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "execution_started_at",
                table: "rounds");

            migrationBuilder.DropColumn(
                name: "execution_status",
                table: "rounds");

            migrationBuilder.DropColumn(
                name: "session_heartbeat_at",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "session_host",
                table: "matches");

            migrationBuilder.CreateIndex(
                name: "ix_matches_fixture_id",
                table: "matches",
                column: "fixture_id");

            migrationBuilder.Sql("""
                UPDATE rounds
                   SET execution_status = 'Scheduled'
                 WHERE completed_at IS NULL
                   AND execution_status = 'Completed';
                """);
        }
    }
}
