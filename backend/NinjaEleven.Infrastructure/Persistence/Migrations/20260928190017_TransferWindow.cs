using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Makes a deal say what it is about without needing a row that does not exist yet.
    ///
    /// Three changes, and each of them is a thing the market could not ask before:
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <c>selling_club_id</c> becomes optional, because a player with no club is signed and
    ///     there is nobody to sell him to. A transfer without a seller is not a broken transfer;
    ///     it is a free agent joining a club for nothing.
    ///   </item>
    ///   <item>
    ///     <c>arrival_season_number</c> is added and <c>arrival_season_id</c> becomes optional,
    ///     because a deal names the season it waits for from the moment it is made and the world
    ///     opens that season later. The number is the rule; the row is bookkeeping, and a world
    ///     playing its first season has no second row to point at.
    ///   </item>
    ///   <item>
    ///     <c>teams.is_manager_club</c> is added, because one club in the world has a person
    ///     answering for it and the market is the only thing in the game that reads which.
    ///   </item>
    /// </list>
    /// </summary>
    public partial class TransferWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "selling_club_id",
                table: "transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "arrival_season_id",
                table: "transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "arrival_season_number",
                table: "transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_manager_club",
                table: "teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_transfers_arrival_season_number_status",
                table: "transfers",
                columns: new[] { "arrival_season_number", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transfers_arrival_season_number_status",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "arrival_season_number",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "is_manager_club",
                table: "teams");

            migrationBuilder.AlterColumn<Guid>(
                name: "selling_club_id",
                table: "transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "arrival_season_id",
                table: "transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
