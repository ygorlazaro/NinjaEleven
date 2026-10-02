using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixInboxLinkRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The box's doors were wrong in the rows already written, and a route is only ever
            // right in the row that carries it: a manager reading mail from before the fix would
            // still have been sent to the stadium by a button labelled "Ver os patrocinadores",
            // and to the club selector by three buttons whose routes this game has never had.
            //
            // Each value is translated by what the message's own button said, which is why the
            // club's own screens are built out of the row's recipient rather than spelled out:
            // "/elenco" was "Ver o elenco" and "/base" was "Ver a base", and both of those
            // doors are the club with an id in them.
            migrationBuilder.Sql(
                """
                UPDATE inbox_messages
                   SET link_route = CASE link_route
                       WHEN '/elenco'   THEN '/team/' || recipient_team_id::text
                       WHEN '/base'     THEN '/team/' || recipient_team_id::text || '/base'
                       WHEN '/mercado' THEN '/transfer'
                       -- Only the three shirt messages pointed at the ground: a deal that ran
                       -- out, a deal signed and a shortlist of who would take the club.
                       WHEN '/estadio'  THEN '/patrocinadores'
                       ELSE link_route
                   END
                 WHERE link_route IN ('/elenco', '/base', '/mercado', '/estadio');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // There is nothing to undo faithfully: "/elenco", "/mercado" and "/base" are not
            // routes this game has, so putting them back would restore doors that go nowhere.
            // The ground is the one door of the four that did exist, and the shirt messages that
            // pointed at it are recovered from their sender rather than guessed at.
            migrationBuilder.Sql(
                """
                UPDATE inbox_messages
                   SET link_route = '/estadio'
                 WHERE link_route = '/patrocinadores'
                   AND sender_name LIKE 'Marketing%';
                """);
        }
    }
}