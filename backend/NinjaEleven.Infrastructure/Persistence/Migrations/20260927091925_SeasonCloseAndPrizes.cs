using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeasonCloseAndPrizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "finance_movements",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reference",
                table: "finance_movements");
        }
    }
}
