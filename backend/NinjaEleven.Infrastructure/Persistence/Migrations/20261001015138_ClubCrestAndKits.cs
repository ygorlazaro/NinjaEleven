using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClubCrestAndKits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "away_kit_json",
                table: "teams",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "crest_json",
                table: "teams",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_kit_json",
                table: "teams",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "away_kit_side",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "home_kit_side",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "away_kit_json",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "crest_json",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "home_kit_json",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "away_kit_side",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "home_kit_side",
                table: "matches");
        }
    }
}
