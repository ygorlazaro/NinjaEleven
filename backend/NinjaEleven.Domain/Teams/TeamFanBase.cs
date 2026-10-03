namespace NinjaEleven.Domain.Teams;

/// <summary>
/// How many people follow this club, and what the last season did to that number.
///
/// <para>
/// It is a row per club per season rather than a column on the club for the same reason a
/// player's face is on the player and not recomputed on demand: the number is only meaningful
/// as a series. "Thirty-one thousand" is not a fact about a club — it is a fact about a club
/// in a particular season, and the season before it is what tells a reader whether the club is
/// growing or shrinking. A column on the club can hold the current number and throws away
/// exactly the part a manager is looking at.
/// </para>
///
/// <para>
/// <b>The opening number is kept as well as the closing one</b>, because a line of six numbers
/// with no origin draws itself. <see cref="OpeningSupporters"/> is what the club started the
/// season with and <see cref="Supporters"/> is what it finished with, so a screen can print a
/// delta without dividing two rows it happened to have loaded.
/// </para>
///
/// <para>
/// The peak is what happened <em>during</em> the season, and it is deliberately not what the
/// next season starts from. A club whose season went brilliantly in March has a bigger peak than
/// it has a base, and a base that followed the peak would make one good afternoon worth a year of
/// growth — so the peak is a fact about a season and the base is a fact about a club.
/// </para>
/// </summary>
public class TeamFanBase
{
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid SeasonId { get; private set; }

    /// <summary>How many people followed this club at the end of the season.</summary>
    public int Supporters { get; private set; }

    /// <summary>How many followed it when the season opened.</summary>
    public int OpeningSupporters { get; private set; }

    /// <summary>The most anyone ever held on to it during the season.</summary>
    public int PeakSupporters { get; private set; }

    /// <summary>When this row was last written.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Whether the club's following went up over the season.</summary>
    public bool Grew => Supporters > OpeningSupporters;

    /// <summary>How much of a season's season closed.</summary>
    public int Change => Supporters - OpeningSupporters;

    private TeamFanBase() { }

    public static TeamFanBase Create(
        Guid teamId,
        Guid seasonId,
        int supporters,
        DateTimeOffset now)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("A crowd belongs to a club.", nameof(teamId));
        if (seasonId == Guid.Empty) throw new ArgumentException("A crowd is counted over a season.", nameof(seasonId));

        return new TeamFanBase
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            SeasonId = seasonId,
            Supporters = Math.Max(FanBaseRules.SmallestFanBase, supporters),
            OpeningSupporters = Math.Max(FanBaseRules.SmallestFanBase, supporters),
            PeakSupporters = Math.Max(FanBaseRules.SmallestFanBase, supporters),
            UpdatedAt = now
        };
    }

    /// <summary>
    /// The crowd a match played in the middle of this season was worth, held as the season's peak.
    ///
    /// It can only go up: a season's peak is the best moment anybody had, and a later ordinary
    /// Saturday is not evidence that the March derby was smaller than it was.
    /// </summary>
    public void RaiseThePeakTo(int supporters) =>
        PeakSupporters = Math.Max(PeakSupporters, Math.Max(FanBaseRules.SmallestFanBase, supporters));

    /// <summary>
    /// Writes what the season closed at, once.
    ///
    /// <para>
    /// The rules that decide the number live in <see cref="FanBaseRules"/> and are applied by the
    /// service before this is called; this only records the answer. A season that is closed twice
    /// — once by its last match, once by a process that was down over the weekend — writes the
    /// same season's own growth a second time, so the caller asks whether the row already exists
    /// and this does not carry a guard of its own. Two guards for one fact is one guard too many,
    /// and the guard that lives with the row is the one that cannot be forgotten.
    /// </para>
    /// </summary>
    public void CloseTheSeasonWith(int supporters, int peakSupporters, DateTimeOffset now)
    {
        Supporters = Math.Max(FanBaseRules.SmallestFanBase, supporters);
        PeakSupporters = Math.Max(Supporters, Math.Max(FanBaseRules.SmallestFanBase, peakSupporters));
        UpdatedAt = now;
    }
}