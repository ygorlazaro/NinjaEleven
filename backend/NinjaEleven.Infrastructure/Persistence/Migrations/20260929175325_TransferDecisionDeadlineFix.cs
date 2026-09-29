using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferDecisionDeadlineFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "answer_by_round",
                table: "transfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "proposal_round_number",
                table: "transfers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "answer_by_round",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "proposal_round_number",
                table: "transfers");
        }
    }
}
