using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractStartSeasonFirstSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every contract in the world so far was signed by the seeder opening the first
            // season, so every existing row belongs to that season and the column it was just
            // given has to be told so. Without this a squad would read as two seasons into a
            // three-season deal and a rival would pay a fine for signing a man who is free.
            migrationBuilder.Sql("UPDATE team_memberships SET start_season_number = 1;");

            migrationBuilder.AlterColumn<int>(
                name: "start_season_number",
                table: "team_memberships",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "start_season_number",
                table: "team_memberships",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }
    }
}
