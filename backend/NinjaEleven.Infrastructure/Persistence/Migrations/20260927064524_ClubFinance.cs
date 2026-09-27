using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClubFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contract_seasons",
                table: "team_memberships",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "injuries",
                table: "player_season_states",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "finance_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    match_day_number = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finance_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_finance_movements_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_finance_movements_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_finance_movements_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_finance_movements_match_id",
                table: "finance_movements",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_finance_movements_season_id",
                table: "finance_movements",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_finance_movements_team_id_season_id_sequence",
                table: "finance_movements",
                columns: new[] { "team_id", "season_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_finance_movements_team_id_sequence",
                table: "finance_movements",
                columns: new[] { "team_id", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_movements");

            migrationBuilder.DropColumn(
                name: "contract_seasons",
                table: "team_memberships");

            migrationBuilder.DropColumn(
                name: "injuries",
                table: "player_season_states");
        }
    }
}
