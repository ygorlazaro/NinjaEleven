using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowFixtureReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_matches_fixture_id",
                table: "matches");

            migrationBuilder.CreateIndex(
                name: "ix_matches_fixture_id",
                table: "matches",
                column: "fixture_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_matches_fixture_id",
                table: "matches");

            migrationBuilder.CreateIndex(
                name: "ix_matches_fixture_id",
                table: "matches",
                column: "fixture_id",
                unique: true);
        }
    }
}
