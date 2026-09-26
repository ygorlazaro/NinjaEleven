namespace NinjaEleven.Domain.Enums;

/// <summary>
/// The order a squad is read in: goalkeepers, then defenders, then midfielders, then
/// attackers, and alphabetically inside each group. It is a presentation order, shared so
/// that the API, the engine and the client never disagree about how a team lines up on a
/// page.
/// </summary>
public static class PositionOrder
{
    public static int Of(Position position) => position switch
    {
        Position.GK => 0,
        Position.DEF => 1,
        Position.MID => 2,
        Position.ATT => 3,
        _ => 4
    };

    /// <summary>
    /// Orders players by position and, inside a position, by name.
    /// </summary>
    public static IOrderedEnumerable<T> Apply<T>(
        this IEnumerable<T> players,
        Func<T, Position> position,
        Func<T, string> name) =>
        players
            .OrderBy(player => Of(position(player)))
            .ThenBy(name, StringComparer.OrdinalIgnoreCase);
}
