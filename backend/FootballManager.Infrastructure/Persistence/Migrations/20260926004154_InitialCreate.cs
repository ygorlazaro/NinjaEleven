using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FootballManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "names",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_names", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    position = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    speed = table.Column<int>(type: "integer", nullable: false),
                    accuracy = table.Column<int>(type: "integer", nullable: false),
                    dribbling = table.Column<int>(type: "integer", nullable: false),
                    heading = table.Column<int>(type: "integer", nullable: false),
                    strength = table.Column<int>(type: "integer", nullable: false),
                    goalkeeper_power = table.Column<int>(type: "integer", nullable: false),
                    reflexes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "surnames",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_surnames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    short_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    primary_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    secondary_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "competition_seasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_seasons", x => x.id);
                    table.ForeignKey(
                        name: "fk_competition_seasons_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_competition_seasons_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_season_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    energy = table.Column<int>(type: "integer", nullable: false),
                    goals = table.Column<int>(type: "integer", nullable: false),
                    yellow_cards = table.Column<int>(type: "integer", nullable: false),
                    red_cards = table.Column<int>(type: "integer", nullable: false),
                    suspension_matches = table.Column<int>(type: "integer", nullable: false),
                    injury = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_season_states", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_season_states_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_player_season_states_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_player_season_states_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_memberships_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_memberships_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competition_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_competition_participants_competition_seasons_competition_se",
                        column: x => x.competition_season_id,
                        principalTable: "competition_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_competition_participants_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_rounds_competition_seasons_competition_season_id",
                        column: x => x.competition_season_id,
                        principalTable: "competition_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixtures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    away_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixtures", x => x.id);
                    table.ForeignKey(
                        name: "fk_fixtures_rounds_round_id",
                        column: x => x.round_id,
                        principalTable: "rounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fixtures_teams_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fixtures_teams_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fixture_id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    away_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    half = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    current_minute = table.Column<int>(type: "integer", nullable: false),
                    home_score = table.Column<int>(type: "integer", nullable: false),
                    away_score = table.Column<int>(type: "integer", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    seed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_matches_teams_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_teams_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    minute = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    secondary_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    home_score = table.Column<int>(type: "integer", nullable: false),
                    away_score = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_events_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_statistics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_possession = table.Column<int>(type: "integer", nullable: false),
                    away_possession = table.Column<int>(type: "integer", nullable: false),
                    home_shots = table.Column<int>(type: "integer", nullable: false),
                    away_shots = table.Column<int>(type: "integer", nullable: false),
                    home_shots_on_target = table.Column<int>(type: "integer", nullable: false),
                    away_shots_on_target = table.Column<int>(type: "integer", nullable: false),
                    home_corners = table.Column<int>(type: "integer", nullable: false),
                    away_corners = table.Column<int>(type: "integer", nullable: false),
                    home_fouls = table.Column<int>(type: "integer", nullable: false),
                    away_fouls = table.Column<int>(type: "integer", nullable: false),
                    home_yellow_cards = table.Column<int>(type: "integer", nullable: false),
                    away_yellow_cards = table.Column<int>(type: "integer", nullable: false),
                    home_red_cards = table.Column<int>(type: "integer", nullable: false),
                    away_red_cards = table.Column<int>(type: "integer", nullable: false),
                    home_saves = table.Column<int>(type: "integer", nullable: false),
                    away_saves = table.Column<int>(type: "integer", nullable: false),
                    home_goals = table.Column<int>(type: "integer", nullable: false),
                    away_goals = table.Column<int>(type: "integer", nullable: false),
                    home_substitutions = table.Column<int>(type: "integer", nullable: false),
                    away_substitutions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_statistics", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_statistics_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_competition_participants_competition_season_id_team_id",
                table: "competition_participants",
                columns: new[] { "competition_season_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_participants_team_id",
                table: "competition_participants",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_competition_seasons_competition_id_season_id",
                table: "competition_seasons",
                columns: new[] { "competition_id", "season_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_seasons_season_id",
                table: "competition_seasons",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixtures_away_team_id",
                table: "fixtures",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixtures_home_team_id",
                table: "fixtures",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixtures_round_id",
                table: "fixtures",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_events_match_id_sequence",
                table: "match_events",
                columns: new[] { "match_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_statistics_match_id",
                table: "match_statistics",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_away_team_id",
                table: "matches",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_fixture_id",
                table: "matches",
                column: "fixture_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_home_team_id",
                table: "matches",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_status",
                table: "matches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_names_value",
                table: "names",
                column: "value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_season_states_player_id_season_id",
                table: "player_season_states",
                columns: new[] { "player_id", "season_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_season_states_season_id_team_id",
                table: "player_season_states",
                columns: new[] { "season_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_player_season_states_team_id",
                table: "player_season_states",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_players_name",
                table: "players",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_rounds_competition_season_id_number",
                table: "rounds",
                columns: new[] { "competition_season_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seasons_name",
                table: "seasons",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_surnames_value",
                table: "surnames",
                column: "value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_team_memberships_player_id",
                table: "team_memberships",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_memberships_team_id_start_date",
                table: "team_memberships",
                columns: new[] { "team_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_name",
                table: "teams",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_participants");

            migrationBuilder.DropTable(
                name: "match_events");

            migrationBuilder.DropTable(
                name: "match_statistics");

            migrationBuilder.DropTable(
                name: "names");

            migrationBuilder.DropTable(
                name: "player_season_states");

            migrationBuilder.DropTable(
                name: "surnames");

            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "fixtures");

            migrationBuilder.DropTable(
                name: "rounds");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "competition_seasons");

            migrationBuilder.DropTable(
                name: "competitions");

            migrationBuilder.DropTable(
                name: "seasons");
        }
    }
}
