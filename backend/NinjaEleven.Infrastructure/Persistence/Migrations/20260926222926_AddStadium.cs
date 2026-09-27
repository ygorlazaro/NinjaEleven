using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStadium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "stadium_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stadiums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 5000),
                    ticket_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 10m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stadiums", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_teams_stadium_id",
                table: "teams",
                column: "stadium_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stadiums_club_id",
                table: "stadiums",
                column: "club_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_teams_stadiums_stadium_id",
                table: "teams",
                column: "stadium_id",
                principalTable: "stadiums",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_teams_stadiums_stadium_id",
                table: "teams");

            migrationBuilder.DropTable(
                name: "stadiums");

            migrationBuilder.DropIndex(
                name: "ix_teams_stadium_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "stadium_id",
                table: "teams");
        }
    }
}
