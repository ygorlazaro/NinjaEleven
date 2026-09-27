using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FootballManagerPyramid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_competition_seasons_competition_id_season_id",
                table: "competition_seasons");

            migrationBuilder.RenameColumn(
                name: "gate_revenue",
                table: "matches",
                newName: "home_revenue");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "stadiums",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "number",
                table: "seasons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "match_day_id",
                table: "rounds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "window",
                table: "rounds",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "away_revenue",
                table: "matches",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "competition_type",
                table: "matches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "gross_revenue",
                table: "matches",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ticket_price",
                table: "matches",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "window",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "division_id",
                table: "competition_seasons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cup_ties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    home_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    away_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_leg_fixture_id = table.Column<Guid>(type: "uuid", nullable: true),
                    second_leg_fixture_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aggregate_home_goals = table.Column<int>(type: "integer", nullable: true),
                    aggregate_away_goals = table.Column<int>(type: "integer", nullable: true),
                    home_penalty_goals = table.Column<int>(type: "integer", nullable: true),
                    away_penalty_goals = table.Column<int>(type: "integer", nullable: true),
                    winner_team_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cup_ties", x => x.id);
                    table.ForeignKey(
                        name: "fk_cup_ties_competition_seasons_competition_season_id",
                        column: x => x.competition_season_id,
                        principalTable: "competition_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cup_ties_fixtures_first_leg_fixture_id",
                        column: x => x.first_leg_fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_cup_ties_fixtures_second_leg_fixture_id",
                        column: x => x.second_leg_fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_cup_ties_teams_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cup_ties_teams_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cup_ties_teams_winner_team_id",
                        column: x => x.winner_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "divisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_divisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_days", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_days_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trophy_awards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    division_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    prize_money = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    won_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trophy_awards", x => x.id);
                    table.ForeignKey(
                        name: "fk_trophy_awards_competition_seasons_competition_season_id",
                        column: x => x.competition_season_id,
                        principalTable: "competition_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trophy_awards_divisions_division_id",
                        column: x => x.division_id,
                        principalTable: "divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trophy_awards_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trophy_awards_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_seasons_number",
                table: "seasons",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rounds_match_day_id",
                table: "rounds",
                column: "match_day_id");

            migrationBuilder.CreateIndex(
                name: "ix_competition_seasons_competition_id_season_id_division_id",
                table: "competition_seasons",
                columns: new[] { "competition_id", "season_id", "division_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_seasons_division_id",
                table: "competition_seasons",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_away_team_id",
                table: "cup_ties",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_competition_season_id_round_number",
                table: "cup_ties",
                columns: new[] { "competition_season_id", "round_number" });

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_first_leg_fixture_id",
                table: "cup_ties",
                column: "first_leg_fixture_id");

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_home_team_id",
                table: "cup_ties",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_second_leg_fixture_id",
                table: "cup_ties",
                column: "second_leg_fixture_id");

            migrationBuilder.CreateIndex(
                name: "ix_cup_ties_winner_team_id",
                table: "cup_ties",
                column: "winner_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_divisions_name",
                table: "divisions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_divisions_tier",
                table: "divisions",
                column: "tier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_days_season_id_number",
                table: "match_days",
                columns: new[] { "season_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trophy_awards_competition_season_id",
                table: "trophy_awards",
                column: "competition_season_id");

            migrationBuilder.CreateIndex(
                name: "ix_trophy_awards_division_id",
                table: "trophy_awards",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_trophy_awards_season_id",
                table: "trophy_awards",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_trophy_awards_team_id_season_id",
                table: "trophy_awards",
                columns: new[] { "team_id", "season_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_competition_seasons_divisions_division_id",
                table: "competition_seasons",
                column: "division_id",
                principalTable: "divisions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_rounds_match_days_match_day_id",
                table: "rounds",
                column: "match_day_id",
                principalTable: "match_days",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_competition_seasons_divisions_division_id",
                table: "competition_seasons");

            migrationBuilder.DropForeignKey(
                name: "fk_rounds_match_days_match_day_id",
                table: "rounds");

            migrationBuilder.DropTable(
                name: "cup_ties");

            migrationBuilder.DropTable(
                name: "match_days");

            migrationBuilder.DropTable(
                name: "trophy_awards");

            migrationBuilder.DropTable(
                name: "divisions");

            migrationBuilder.DropIndex(
                name: "ix_seasons_number",
                table: "seasons");

            migrationBuilder.DropIndex(
                name: "ix_rounds_match_day_id",
                table: "rounds");

            migrationBuilder.DropIndex(
                name: "ix_competition_seasons_competition_id_season_id_division_id",
                table: "competition_seasons");

            migrationBuilder.DropIndex(
                name: "ix_competition_seasons_division_id",
                table: "competition_seasons");

            migrationBuilder.DropColumn(
                name: "name",
                table: "stadiums");

            migrationBuilder.DropColumn(
                name: "number",
                table: "seasons");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "rounds");

            migrationBuilder.DropColumn(
                name: "match_day_id",
                table: "rounds");

            migrationBuilder.DropColumn(
                name: "window",
                table: "rounds");

            migrationBuilder.DropColumn(
                name: "away_revenue",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "competition_type",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "gross_revenue",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "ticket_price",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "window",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "division_id",
                table: "competition_seasons");

            migrationBuilder.RenameColumn(
                name: "home_revenue",
                table: "matches",
                newName: "gate_revenue");

            migrationBuilder.CreateIndex(
                name: "ix_competition_seasons_competition_id_season_id",
                table: "competition_seasons",
                columns: new[] { "competition_id", "season_id" },
                unique: true);
        }
    }
}
