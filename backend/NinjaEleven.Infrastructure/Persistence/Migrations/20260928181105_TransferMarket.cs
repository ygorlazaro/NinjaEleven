using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "team_id",
                table: "player_season_states",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "retiring",
                table: "player_season_states",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selling_club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buying_club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arrival_season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    proposed_at = table.Column<DateOnly>(type: "date", precision: 14, scale: 2, nullable: false),
                    resolved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    arrival_round_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfers", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfers_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfers_seasons_arrival_season_id",
                        column: x => x.arrival_season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfers_seasons_proposal_season_id",
                        column: x => x.proposal_season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfers_teams_buying_club_id",
                        column: x => x.buying_club_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfers_teams_selling_club_id",
                        column: x => x.selling_club_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transfers_arrival_season_id",
                table: "transfers",
                column: "arrival_season_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_buying_club_id",
                table: "transfers",
                column: "buying_club_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_player_id",
                table: "transfers",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_proposal_season_id_status",
                table: "transfers",
                columns: new[] { "proposal_season_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_transfers_selling_club_id_buying_club_id_player_id",
                table: "transfers",
                columns: new[] { "selling_club_id", "buying_club_id", "player_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfers");

            migrationBuilder.DropColumn(
                name: "retiring",
                table: "player_season_states");

            migrationBuilder.AlterColumn<Guid>(
                name: "team_id",
                table: "player_season_states",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
