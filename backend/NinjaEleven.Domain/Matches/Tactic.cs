using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// One line of a tactic: how many men play in it, and what the line is for.
/// </summary>
/// <param name="Position">The line in the engine's three bands.</param>
/// <param name="Count">How many of them.</param>
/// <param name="Profile">
/// What the line is for, which is what makes two tactics with the same numbers different
/// eleven. A 4-2-3-1 and a 4-5-1 are both four defenders, five midfielders and one
/// striker, and they are not the same team: one of the five has to hold, one has to
/// create. Saying so is the difference between a list of numbers and a tactic.
/// </param>
public sealed record TacticLine(Position Position, int Count, TacticProfile Profile = TacticProfile.Balanced);

/// <summary>
/// What a line of the eleven exists to do.
/// </summary>
public enum TacticProfile
{
    /// <summary>Hold the shape and win the ball back.</summary>
    Defensive,

    /// <summary>Do everything, and nothing in particular.</summary>
    Balanced,

    /// <summary>Get forward and make the thing happen.</summary>
    Attacking
}

/// <summary>
/// A shape the manager can order his eleven to be built in.
///
/// It is a list of lines rather than a string like "4-2-3-1" because the string is what a
/// manager says and the list is what has to be filled: eleven men, one keeper, and every
/// other name accounted for by a line. A tactic that does not add up to eleven is not a
/// tactic, and <see cref="IsComplete"/> is how the catalogue keeps itself honest.
/// </summary>
public sealed record Tactic(string Code, string Name, IReadOnlyList<TacticLine> Lines)
{
    /// <summary>Every man on the pitch, keeper included.</summary>
    public int Size => Lines.Sum(line => line.Count);

    public int Defenders => Count(Position.DEF);

    public int Midfielders => Count(Position.MID);

    public int Attackers => Count(Position.ATT);

    /// <summary>
    /// Whether this shape is a whole eleven. Every tactic in the catalogue is, and a new
    /// one that is not is a mistake rather than an option.
    /// </summary>
    public bool IsComplete => Size == 11 && Lines.Any(line => line.Position == Position.GK && line.Count == 1);

    private int Count(Position position) =>
        Lines.Where(line => line.Position == position).Sum(line => line.Count);
}

/// <summary>
/// The tactics a manager can choose between.
///
/// Ten of them, because a manager who only has one option has a default rather than a
/// decision. They are not ten names for the same eleven: a back four and a back five
/// produce different sides, and among the shapes with the same numbers the profiles pick
/// different men — a holding midfielder and an attacking one are not interchangeable even
/// when there are five of them either way.
/// </summary>
public static class Tactics
{
    public static IReadOnlyList<Tactic> All { get; } =
    [
        new("442", "4-4-2",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 4),
            Line(Position.MID, 2, TacticProfile.Defensive),
            Line(Position.MID, 2, TacticProfile.Attacking),
            Line(Position.ATT, 2)
        ]),

        new("433", "4-3-3",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 4),
            Line(Position.MID, 3),
            Line(Position.ATT, 3)
        ]),

        new("4231", "4-2-3-1",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 4),
            Line(Position.MID, 2, TacticProfile.Defensive),
            Line(Position.MID, 3, TacticProfile.Attacking),
            Line(Position.ATT, 1)
        ]),

        new("541", "5-4-1",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 5),
            Line(Position.MID, 4, TacticProfile.Defensive),
            Line(Position.ATT, 1)
        ]),

        new("532", "5-3-2",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 5),
            Line(Position.MID, 3),
            Line(Position.ATT, 2)
        ]),

        new("352", "3-5-2",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 3),
            Line(Position.MID, 2, TacticProfile.Defensive),
            Line(Position.MID, 3, TacticProfile.Attacking),
            Line(Position.ATT, 2)
        ]),

        new("343", "3-4-3",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 3),
            Line(Position.MID, 4),
            Line(Position.ATT, 3)
        ]),

        new("451", "4-5-1",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 4),
            Line(Position.MID, 5),
            Line(Position.ATT, 1)
        ]),

        new("4141", "4-1-4-1",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 4),
            Line(Position.MID, 1, TacticProfile.Defensive),
            Line(Position.MID, 4, TacticProfile.Attacking),
            Line(Position.ATT, 1)
        ]),

        new("334", "3-3-4",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, 3),
            Line(Position.MID, 3),
            Line(Position.ATT, 4, TacticProfile.Attacking)
        ])
    ];

    /// <summary>
    /// The shape used when nobody asked for one: the club is made of, which is what
    /// <see cref="Formation.FromSquadComposition"/> reads off the players it has.
    /// </summary>
    public static Tactic Default { get; } = FromComposition(4, 4, 3);

    /// <summary>
    /// Looks a tactic up by its code, case-insensitively, because the code comes from a
    /// query string and a manager who typed "442" and a client that sent "442" are the
    /// same request.
    /// </summary>
    public static Tactic? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(tactic =>
                string.Equals(tactic.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The shape a squad plays when the manager ordered nothing: read off the men he has,
    /// scaled to ten.
    /// </summary>
    public static Tactic FromSquad(IEnumerable<MatchPlayerSnapshot> squad)
    {
        if (squad is null) throw new ArgumentNullException(nameof(squad));

        var shape = Formation.FromSquadComposition(squad.Where(player => player.Position != Position.GK).ToList());

        return FromComposition(shape.Defenders, shape.Midfielders, shape.Attackers);
    }

    /// <summary>
    /// The tactic a set of names is actually playing, as the nearest of the ten.
    ///
    /// This is what the engine measures the eleven by, so a manager who hand-picks
    /// something the catalogue has a name for is told which tactic he has chosen, and one
    /// who picks something it does not is measured on what he actually did rather than on
    /// the nearest round number.
    /// </summary>
    public static Tactic Nearest(int defenders, int midfielders, int attackers) =>
        FromComposition(defenders, midfielders, attackers);

    private static Tactic FromComposition(int defenders, int midfielders, int attackers) =>
        new("custom", "Elenco do clube",
        [
            Line(Position.GK, 1),
            Line(Position.DEF, defenders),
            Line(Position.MID, midfielders),
            Line(Position.ATT, attackers)
        ]);

    private static TacticLine Line(Position position, int count, TacticProfile profile = TacticProfile.Balanced) =>
        new(position, count, profile);
}
