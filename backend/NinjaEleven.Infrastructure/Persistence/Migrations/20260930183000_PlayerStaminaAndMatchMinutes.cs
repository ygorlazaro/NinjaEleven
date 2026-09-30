using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayerStaminaAndMatchMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stamina is a body fact on the player, so the default the column needs is the
            // middle of the scale rather than zero. Zero would be a legal value and a wrong
            // one: it is the worst tank in the world, and a career that already exists would
            // come out of this migration with its entire population unable to finish a match.
            migrationBuilder.AddColumn<int>(
                name: "stamina",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "minutes_played",
                table: "match_player_statistics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // An existing career was seeded before stamina existed, so its players have none.
            // They are given the same curve the seeder draws, from the age they already have,
            // rather than the flat default: a backfill that gave every man of every age the
            // same tank would make a thirty-four-year-old keeper and a twenty-year-old
            // winger indistinguishable in the one place where age is supposed to matter.
            //
            // This is an ALTER rather than a C# backfill because it is one statement over
            // every player, and a query per player to write a column that is a pure function
            // of a column already in the row is a round trip per player for arithmetic.
            migrationBuilder.Sql(
                """
                UPDATE players
                SET stamina = GREATEST(1, LEAST(100,
                    CASE
                        WHEN age <= 19 THEN 62
                        WHEN age <= 23 THEN 72
                        WHEN age <= 29 THEN 84
                        WHEN age <= 33 THEN 74
                        WHEN age <= 36 THEN 60
                        ELSE 48
                    END))
                """);

            // The minutes of a match already played are not recoverable: the engine never
            // wrote them, and reconstructing them from the appearance flags would be a guess
            // wearing the costume of a measurement — which is exactly what the column's own
            // documentation refused to do. Zero is the honest value, and it is the right one
            // for the rows it applies to: a career already on the shelf cannot be re-recovered
            // on the strength of a number nobody kept.
            //
            // What a match played from here on records is the real thing, and the recovery the
            // match applies is computed from the same two stamps — so a season's record and a
            // season's fatigue agree from the first window onwards.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stamina",
                table: "players");

            migrationBuilder.DropColumn(
                name: "minutes_played",
                table: "match_player_statistics");
        }
    }
}
