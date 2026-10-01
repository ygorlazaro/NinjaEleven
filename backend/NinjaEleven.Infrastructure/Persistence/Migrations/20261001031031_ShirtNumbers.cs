using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShirtNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "shirt_number",
                table: "team_memberships",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_team_memberships_team_id_shirt_number",
                table: "team_memberships",
                columns: new[] { "team_id", "shirt_number" },
                unique: true,
                filter: "\"end_date\" IS NULL AND \"shirt_number\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_team_memberships_team_id_shirt_number",
                table: "team_memberships");

            migrationBuilder.DropColumn(
                name: "shirt_number",
                table: "team_memberships");
        }
    }
}
