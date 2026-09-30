using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrainingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "training_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    match_day_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attribute = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    energy_cost = table.Column<int>(type: "integer", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_training_sessions_match_days_match_day_id",
                        column: x => x.match_day_id,
                        principalTable: "match_days",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_training_sessions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_training_sessions_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_sessions_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_match_day_id",
                table: "training_sessions",
                column: "match_day_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_player_id",
                table: "training_sessions",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_season_id_team_id_day",
                table: "training_sessions",
                columns: new[] { "season_id", "team_id", "day" });

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_team_id_day",
                table: "training_sessions",
                columns: new[] { "team_id", "day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "training_sessions");
        }
    }
}
