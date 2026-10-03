using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// The shape of an eleven, as the three numbers outfield football is counted in.
///
/// It is not a setting a manager picks and the engine obeys. It is read off the eleven:
/// four defenders, three midfielders and three attackers is a shape, and the club that
/// puts eight men at the back has chosen a different one by picking a different eleven.
/// The engine recomputes it after every substitution for the same reason, which is why a
/// club that replaces a striker for a defender is playing a different match afterwards.
/// </summary>
public readonly record struct Formation(int Defenders, int Midfielders, int Attackers)
{
    /// <summary>
    /// The shape used before a real eleven is known, and as a floor for the tests.
    /// </summary>
    public static readonly Formation Default = new(4, 3, 3);

    /// <summary>
    /// The shape of a team sheet, read from the positions of the men named in it. A
    /// goalkeeper is not part of it: the three numbers add up to the ten outfield players
    /// whatever the club decided to do with them.
    /// </summary>
    /// <remarks>
    /// It counts the eleven it is handed and not the men still standing, because those are
    /// two questions and this is the first one. A striker who is carried off, or a defender
    /// who is shown a red card, changes how many men a club has and not the shape it is
    /// playing; counting the survivors instead reported a three-four-three that had lost a
    /// striker as a four-three-nothing — seven outfield players on a pitch of ten — so a
    /// manager who ordered 3-4-3 was told his club had come out in another shape because
    /// somebody got hurt. How many men are actually playing is the strength's business
    /// (<see cref="TeamStrength"/>), which filters for itself; the shape is the team
    /// sheet's.
    /// </remarks>
    public static Formation FromComposition(IEnumerable<MatchPlayerSnapshot> eleven)
    {
        var teamSheet = eleven.ToList();

        return new Formation(
            teamSheet.Count(player => player.Position == Position.DEF),
            teamSheet.Count(player => player.Position == Position.MID),
            teamSheet.Count(player => player.Position == Position.ATT));
    }

    /// <summary>
    /// A shape whose three numbers are one team's worth of outfield players. Used when
    /// a club is so short of men that there is nothing to count.
    /// </summary>
    public static Formation Empty => new(0, 0, 0);

    /// <summary>
    /// The shape a club naturally plays in, read off the players it has to choose from and
    /// scaled to the ten outfield places on a team sheet.
    ///
    /// A club is not a bag of footballers: eight defenders and two forwards is a different
    /// club from four of each, and it should not put out the same eleven as its neighbour.
    /// Reading the shape off the squad is what makes that true — the engine picks the best
    /// men for the lines the club is made of, and the lines the club is made of are what
    /// then decides the match.
    ///
    /// A club with fewer than ten outfield players keeps the shape it has: there is
    /// nothing to scale and nothing to choose between.
    /// </summary>
    public static Formation FromSquadComposition(IReadOnlyCollection<MatchPlayerSnapshot> squad)
    {
        if (squad is null) throw new ArgumentNullException(nameof(squad));

        var lines = new[] { Position.DEF, Position.MID, Position.ATT };
        var available = lines.Select(position => squad.Count(player => player.Position == position)).ToArray();
        var total = available.Sum();

        if (total == 0)
        {
            return Default;
        }

        if (total < OutfieldPlaces)
        {
            return new Formation(available[0], available[1], available[2]);
        }

        // Floor first, so the rounding never asks a line for a man it does not have, and
        // then hand out what the floors took, largest line first.
        var assigned = lines
            .Select((_, index) => (int)Math.Floor(available[index] * (double)OutfieldPlaces / total))
            .ToArray();

        var seats = assigned.Sum();
        for (var index = 0; seats < OutfieldPlaces; index = (index + 1) % lines.Length)
        {
            if (assigned[index] < available[index])
            {
                assigned[index]++;
                seats++;
            }
        }

        return new Formation(assigned[0], assigned[1], assigned[2]);
    }

    /// <summary>
    /// How many outfield players a team sheet has places for.
    /// </summary>
    private const int OutfieldPlaces = 10;

    public bool IsEmpty => Defenders == 0 && Midfielders == 0 && Attackers == 0;

    public int Outfielders => Defenders + Midfielders + Attackers;

    /// <summary>
    /// The shape as it is written on a team sheet.
    /// </summary>
    public override string ToString() => $"{Defenders}-{Midfielders}-{Attackers}";
}
