using System.Reflection;
using NinjaEleven.Domain.Inbox;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The doors a message opens, held against the doors the game has.
///
/// A message's button is a promise: it says where the manager is going. A route that does not
/// exist does not fail — the frontend's catch-all answers it by sending him to the club
/// selector — so the cost of a wrong one is silent, which is exactly why it has to be a test.
///
/// <para>
/// The routes below are the frontend's own table, copied. It cannot be read from here, so the
/// copy has to be kept in step by hand: a new screen added to <c>main.tsx</c> and not to this
/// list shows up as a door this test refuses, which is the direction the mistake should fail.
/// </para>
/// </summary>
public class InboxLinkTests
{
    /// <summary>
    /// Every route of <c>src/main.tsx</c>, in the pattern the router writes, with the colon the
    /// router uses for a parameter.
    /// </summary>
    private static readonly string[] TheGamesRoutes =
    [
        "/",
        "/login",
        "/register",
        "/league",
        "/copa",
        "/calendar",
        "/financeiro",
        "/caixa",
        "/tactics",
        "/club",
        "/estadio",
        "/patrocinadores",
        "/artilheiros",
        "/ranking",
        "/transfer",
        "/team/:teamId",
        "/team/:teamId/base",
        "/player/:playerId",
        "/match/:matchId"
    ];

    /// <summary>
    /// A link built from an id is a route the router fills in, and it is compared as a pattern:
    /// an id, a <c>{placeholder}</c> and a router parameter all become <c>:param</c>, so a door
    /// added with an id and a door written in the router's own vocabulary are one assertion.
    /// </summary>
    private static string AsRoutePattern(string route) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(
                route.Replace(Guid.Empty.ToString(), ":param"),
                @"\{[^}]+\}",
                ":param"),
            @":[A-Za-z_][A-Za-z0-9_]*",
            ":param");

    [Fact]
    public void Every_door_the_box_has_is_a_door_the_game_has()
    {
        var open = typeof(InboxLink)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(
            [
                InboxLink.Academy(Guid.Empty),
                InboxLink.Match(Guid.Empty),
                InboxLink.Team(Guid.Empty)
            ])
            .Select(AsRoutePattern);

        var theGamesDoors = TheGamesRoutes.Select(AsRoutePattern);

        Assert.All(open, door => Assert.Contains(door, theGamesDoors));
    }

    /// <summary>
    /// A shirt is signed on the sponsors screen, and a message about the shirt says so.
    ///
    /// It was the ground, which is a real route and therefore worse: the manager pressed a
    /// button labelled "Ver os patrocinadores" and arrived somewhere that exists and is about
    /// something else, with nothing on it saying he had been sent to the wrong page.
    /// </summary>
    [Fact]
    public void The_shirt_is_on_the_sponsors_screen_and_not_on_the_ground()
    {
        Assert.Equal("/patrocinadores", InboxLink.Sponsors);
        Assert.Contains(InboxLink.Sponsors, TheGamesRoutes);
        Assert.NotEqual("/estadio", InboxLink.Sponsors);
    }
}