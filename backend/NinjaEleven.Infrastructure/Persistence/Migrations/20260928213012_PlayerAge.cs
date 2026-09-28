using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayerAge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The age is added first and filled from the birth dates the world was written with,
            // and the birth date is dropped afterwards. The order is the whole migration: a world
            // that had its dates dropped before they were read would come out of it with a
            // thousand players all the same age, and the ages are not something that can be
            // worked out afterwards — the date they were read from is the date that is going.
            migrationBuilder.AddColumn<int>(
                name: "age",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // The same arithmetic the game was reading off a date: the years between the day he
            // was born and today, and not the difference of the two year numbers, which is a
            // year out for anybody whose birthday has not come round yet.
            migrationBuilder.Sql(
                """
                UPDATE players
                SET age = EXTRACT(YEAR FROM age(CURRENT_DATE, birth_date))::int;
                """);

            migrationBuilder.DropColumn(
                name: "birth_date",
                table: "players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "birth_date",
                table: "players",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // A date built back out of the age, so a world rolled back is a world whose players
            // are as old as they were rather than a world of people born on the first of January
            // of the year one.
            migrationBuilder.Sql(
                """
                UPDATE players
                SET birth_date = (CURRENT_DATE - make_interval(years => age))::date;
                """);

            migrationBuilder.DropColumn(
                name: "age",
                table: "players");
        }
    }
}
