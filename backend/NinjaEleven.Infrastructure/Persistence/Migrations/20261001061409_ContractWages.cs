using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractWages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_training_sessions_player_id",
                table: "training_sessions");

            migrationBuilder.DropIndex(
                name: "ix_training_sessions_team_id_day_ordinal",
                table: "training_sessions");

            migrationBuilder.AddColumn<decimal>(
                name: "wage",
                table: "team_memberships",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_player_id_day",
                table: "training_sessions",
                columns: new[] { "player_id", "day" });

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_player_id_day_ordinal",
                table: "training_sessions",
                columns: new[] { "player_id", "day", "ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_training_sessions_player_id_day",
                table: "training_sessions");

            migrationBuilder.DropIndex(
                name: "ix_training_sessions_player_id_day_ordinal",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "wage",
                table: "team_memberships");

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_player_id",
                table: "training_sessions",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_team_id_day_ordinal",
                table: "training_sessions",
                columns: new[] { "team_id", "day", "ordinal" },
                unique: true);
        }
    }
}
