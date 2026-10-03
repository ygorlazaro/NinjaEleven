using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StadiumConstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stadium_constructions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stadium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rounds = table.Column<int>(type: "integer", nullable: false),
                    started_after_round = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stadium_constructions", x => x.id);
                    table.ForeignKey(
                        name: "fk_stadium_constructions_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stadium_constructions_stadiums_stadium_id",
                        column: x => x.stadium_id,
                        principalTable: "stadiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stadium_constructions_teams_club_id",
                        column: x => x.club_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stadium_constructions_club_id",
                table: "stadium_constructions",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_stadium_constructions_completed_at",
                table: "stadium_constructions",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "ix_stadium_constructions_one_open_per_ground",
                table: "stadium_constructions",
                column: "stadium_id",
                unique: true,
                filter: "completed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stadium_constructions_season_id",
                table: "stadium_constructions",
                column: "season_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stadium_constructions");
        }
    }
}
