using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrainingSessionOrdinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added as nullable, filled, then made required: a default of zero would give every
            // session already in the table the same place in its day's allowance, and the
            // unique index below would refuse to be created against them. The backfill numbers
            // each club's sessions in the order they were actually run, which is the order the
            // allowance was spent in.
            migrationBuilder.AddColumn<int>(
                name: "ordinal",
                table: "training_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE training_sessions AS session
                SET ordinal = placed.place
                FROM (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY team_id, day
                               ORDER BY performed_at, id
                           ) - 1 AS place
                    FROM training_sessions
                ) AS placed
                WHERE session.id = placed.id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ordinal",
                table: "training_sessions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_team_id_day_ordinal",
                table: "training_sessions",
                columns: new[] { "team_id", "day", "ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_training_sessions_team_id_day_ordinal",
                table: "training_sessions");

            migrationBuilder.DropColumn(
                name: "ordinal",
                table: "training_sessions");
        }
    }
}
