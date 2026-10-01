using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManagerDormancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dismissed_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "dismissed_team_id",
                table: "users",
                type: "uuid",
                nullable: true);

            // Added non-nullable with a false default, which would have read every account
            // that already existed as a dismissed one. The column is the truth about a
            // dismissal that has not happened, so the backfill has to say "still here".
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_login_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // An account that existed before this column has a last sign-in nobody recorded,
            // and the honest reading of an unknown last sign-in is the day the account was
            // created. Its dormancy clock starts from the same place the clock of a manager
            // who registers and never returns starts, and no account is dismissed by the act
            // of adding the column that measures it.
            //
            // This runs last because it reads the column the line above creates: a backfill
            // that ran first would be an update to a column that does not exist yet, and the
            // migration would fail on the accounts it was written to protect.
            migrationBuilder.Sql(
                """
                UPDATE users
                SET last_login_at = created_at
                WHERE last_login_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dismissed_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "dismissed_team_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "users");
        }
    }
}
