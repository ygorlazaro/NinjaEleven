using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MatchPlayerStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_player_statistics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started = table.Column<bool>(type: "boolean", nullable: false),
                    came_on = table.Column<bool>(type: "boolean", nullable: false),
                    subbed_off = table.Column<bool>(type: "boolean", nullable: false),
                    goals = table.Column<int>(type: "integer", nullable: false),
                    own_goals = table.Column<int>(type: "integer", nullable: false),
                    assists = table.Column<int>(type: "integer", nullable: false),
                    saves = table.Column<int>(type: "integer", nullable: false),
                    yellow_cards = table.Column<int>(type: "integer", nullable: false),
                    red_cards = table.Column<int>(type: "integer", nullable: false),
                    was_injured = table.Column<bool>(type: "boolean", nullable: false),
                    injured_off = table.Column<bool>(type: "boolean", nullable: false),
                    injury_matches_out = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_player_statistics", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_player_statistics_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_player_statistics_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_match_player_statistics_match_id_player_id",
                table: "match_player_statistics",
                columns: new[] { "match_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_player_statistics_player_id_season_id",
                table: "match_player_statistics",
                columns: new[] { "player_id", "season_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_player_statistics");
        }
    }
}
