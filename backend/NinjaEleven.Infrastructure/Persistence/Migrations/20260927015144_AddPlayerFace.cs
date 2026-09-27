using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerFace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "face",
                table: "players",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "face",
                table: "players");
        }
    }
}
