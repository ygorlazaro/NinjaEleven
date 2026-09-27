using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CupTieLoser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "loser_team_id",
                table: "cup_ties",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_loser_team_id",
                table: "cup_ties",
                column: "loser_team_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cup_ties_teams_loser_team_id",
                table: "cup_ties",
                column: "loser_team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cup_ties_teams_loser_team_id",
                table: "cup_ties");

            migrationBuilder.DropIndex(
                name: "ix_cup_ties_loser_team_id",
                table: "cup_ties");

            migrationBuilder.DropColumn(
                name: "loser_team_id",
                table: "cup_ties");
        }
    }
}
