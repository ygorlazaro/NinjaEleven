using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "active_sponsor_contract_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sponsors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    industry = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sponsor_contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sponsor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    per_match_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    contract_matches = table.Column<int>(type: "integer", nullable: false),
                    matches_played = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "text", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsor_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_sponsor_contracts_sponsors_sponsor_id",
                        column: x => x.sponsor_id,
                        principalTable: "sponsors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_teams_active_sponsor_contract_id",
                table: "teams",
                column: "active_sponsor_contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsor_contracts_sponsor_id",
                table: "sponsor_contracts",
                column: "sponsor_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsor_contracts_team_id",
                table: "sponsor_contracts",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsor_contracts_team_id_status",
                table: "sponsor_contracts",
                columns: new[] { "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsors_name",
                table: "sponsors",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_teams_sponsor_contracts_active_sponsor_contract_id",
                table: "teams",
                column: "active_sponsor_contract_id",
                principalTable: "sponsor_contracts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_teams_sponsor_contracts_active_sponsor_contract_id",
                table: "teams");

            migrationBuilder.DropTable(
                name: "sponsor_contracts");

            migrationBuilder.DropTable(
                name: "sponsors");

            migrationBuilder.DropIndex(
                name: "ix_teams_active_sponsor_contract_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "active_sponsor_contract_id",
                table: "teams");
        }
    }
}
