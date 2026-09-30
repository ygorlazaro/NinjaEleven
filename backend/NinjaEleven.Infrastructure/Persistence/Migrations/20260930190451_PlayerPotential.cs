using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayerPotential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "potential",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            // Every player who already exists is given a ceiling of what he is right now, which
            // is the only backfill that cannot invent anything.
            //
            // A default of zero would have been worse than useless: a man whose potential is
            // below his own attributes is at his ceiling by definition, so the whole existing
            // world would have been frozen — no growth, and no training either, because a
            // player is never allowed to be trained past a ceiling he is already above. A
            // default of a hundred would have been the opposite mistake, turning every
            // thirty-one-year-old into a man with a full career ahead of him.
            //
            // The reading is approximated by the strongest attribute rather than computed from
            // the position weights, because a migration cannot ask the domain for a weighted
            // average and a SQL copy of those weights would be a second set of them to keep in
            // step. The approximation only decides where these particular men start; the first
            // season opening recomputes every one of them against the real rule.
            migrationBuilder.Sql(
                """
                UPDATE players
                SET potential = GREATEST(speed, accuracy, dribbling, heading, strength)
                WHERE position <> 'GK';

                UPDATE players
                SET potential = (goalkeeper_power + reflexes) / 2
                WHERE position = 'GK';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "potential",
                table: "players");
        }
    }
}
