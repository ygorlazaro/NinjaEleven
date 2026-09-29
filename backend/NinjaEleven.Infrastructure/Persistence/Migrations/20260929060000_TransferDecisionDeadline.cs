using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferDecisionDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The round the proposal was made in and the round by which the selling club must
            // answer. The deadline is null for a club with a manager, which answers on its own
            // schedule; it is set for a club without one, which has to be told when to decide.
            migrationBuilder.AlterColumn<int?>(
                name: "arrival_round_id",
                table: "transfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int?>(
                name: "proposal_round_number",
                table: "transfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int?>(
                name: "answer_by_round",
                table: "transfers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proposal_round_number",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "answer_by_round",
                table: "transfers");
        }
    }
}