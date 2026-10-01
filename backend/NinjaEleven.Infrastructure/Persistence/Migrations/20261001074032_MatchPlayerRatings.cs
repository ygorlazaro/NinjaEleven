using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MatchPlayerRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "corners_won",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "duels_lost",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "duels_won",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "fouls_committed",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "rating",
                table: "match_player_statistics",
                type: "double precision",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shots_off_target",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "shots_on_target",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "corners_won",
                table: "match_player_statistics");

            migrationBuilder.DropColumn(
                name: "duels_lost",
                table: "match_player_statistics");

            migrationBuilder.DropColumn(
                name: "duels_won",
                table: "match_player_statistics");

            migrationBuilder.DropColumn(
                name: "fouls_committed",
                table: "match_player_statistics");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "match_player_statistics");

            migrationBuilder.DropColumn(
                name: "shots_off_target",
                table: "match_player_statistics");

            migrationBuilder.DropColumn(
                name: "shots_on_target",
                table: "match_player_statistics");
        }
    }
}
