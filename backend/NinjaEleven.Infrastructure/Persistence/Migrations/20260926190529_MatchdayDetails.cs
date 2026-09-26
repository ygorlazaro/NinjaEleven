using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MatchdayDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "away_injuries",
                table: "match_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "away_own_goals",
                table: "match_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "home_injuries",
                table: "match_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "home_own_goals",
                table: "match_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "away_injuries",
                table: "match_statistics");

            migrationBuilder.DropColumn(
                name: "away_own_goals",
                table: "match_statistics");

            migrationBuilder.DropColumn(
                name: "home_injuries",
                table: "match_statistics");

            migrationBuilder.DropColumn(
                name: "home_own_goals",
                table: "match_statistics");
        }
    }
}
