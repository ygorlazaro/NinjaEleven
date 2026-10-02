using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SponsorSizeAndAppeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The columns are added as a company of no particular size, and then every sponsor
            // already in the book is given one. A default of zero would be the worst possible
            // one: a company that wants nothing and would put its name on nothing, so a world
            // migrated onto it would be a world where nobody sponsors anybody.
            migrationBuilder.AddColumn<int>(
                name: "max_clubs",
                table: "sponsors",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "max_tier",
                table: "sponsors",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<double>(
                name: "min_appeal",
                table: "sponsors",
                type: "double precision",
                nullable: false,
                defaultValue: 0.85);

            migrationBuilder.AddColumn<int>(
                name: "weight",
                table: "sponsors",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            // The sizes are dealt out by the sponsor's own name, and the name is hashed with
            // the same FNV-1a the world uses everywhere else (Domain/Common/StableHash) — so a
            // migrated world and a freshly seeded one arrive at the same book. The database's
            // own hashtext would have been a shorter line of SQL and a different answer. The
            // other three columns follow from the size rather than being written out a second
            // time, so there is one place in the world that says what a national company is.
            migrationBuilder.Sql(
                """
                WITH RECURSIVE spelled AS (
                    SELECT id, name, 1 AS pos, 2166136261::bigint AS digest
                    FROM sponsors
                  UNION ALL
                    SELECT id, name, pos + 1,
                           mod((digest # ascii(substr(name, pos, 1))::bigint) * 16777619, 4294967296)
                    FROM spelled
                    WHERE pos <= length(name)
                ),
                sized AS (
                    SELECT id, (spelled.digest % 3 + 1)::int AS weight
                    FROM spelled
                    WHERE pos = length(name)
                )
                UPDATE sponsors
                SET weight = sized.weight,
                    max_clubs = CASE sized.weight WHEN 1 THEN 2 WHEN 3 THEN 8 ELSE 4 END,
                    max_tier = CASE sized.weight WHEN 1 THEN 4 WHEN 3 THEN 2 ELSE 3 END,
                    min_appeal = CASE sized.weight WHEN 1 THEN 0.0 WHEN 3 THEN 1.05 ELSE 0.85 END
                FROM sized
                WHERE sponsors.id = sized.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_clubs",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "max_tier",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "min_appeal",
                table: "sponsors");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "sponsors");
        }
    }
}
