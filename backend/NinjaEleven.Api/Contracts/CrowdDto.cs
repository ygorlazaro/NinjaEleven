using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// One rival, and the games that made it one.
/// </summary>
/// <remarks>
/// The five bands are carried rather than left for a screen to work out. They are the weights
/// the domain already applied, and a client that re-derived them would be a second opinion
/// about what a rivalry is — and a manager reading "close matches" is owed the reason, not the
/// sum.
/// </remarks>
public class RivalDto
{
    public Guid OpponentTeamId { get; init; }

    /// <summary>A name is always a door, so the id is here and not just the text.</summary>
    public string OpponentName { get; init; } = string.Empty;

    public int Meetings { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Defeats { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }

    /// <summary>How much of a rivalry this is, on the scale in <see cref="RivalryRules"/>.</summary>
    public double Score { get; init; }

    public RivalWhyDto Why { get; init; } = new();

    public static RivalDto From(RivalLine rival) => new()
    {
        OpponentTeamId = rival.OpponentTeamId,
        OpponentName = rival.OpponentName,
        Meetings = rival.Meetings,
        Wins = rival.Wins,
        Draws = rival.Draws,
        Defeats = rival.Defeats,
        GoalsFor = rival.GoalsFor,
        GoalsAgainst = rival.GoalsAgainst,
        Score = rival.Score,
        Why = RivalWhyDto.From(rival.Why)
    };
}

/// <summary>The five things a rivalry is made of, each already weighted.</summary>
public class RivalWhyDto
{
    /// <summary>How often the two clubs have met.</summary>
    public double Recurrence { get; init; }

    /// <summary>How close the games were.</summary>
    public double Decisiveness { get; init; }

    /// <summary>How evenly the two clubs have split them.</summary>
    public double Results { get; init; }

    /// <summary>The longest run of games won, or not won, without a break.</summary>
    public double Streaks { get; init; }

    /// <summary>What has been happening between them lately.</summary>
    public double RecentForm { get; init; }

    /// <summary>The weighted sum of the five above, which is the number it is ordered by.</summary>
    public double Overall { get; init; }

    public static RivalWhyDto From(RivalryGrowth growth) => new()
    {
        Recurrence = growth.Recurrence,
        Decisiveness = growth.Decisiveness,
        Results = growth.Results,
        Streaks = growth.Streaks,
        RecentForm = growth.RecentForm,
        Overall = growth.Overall
    };
}

/// <summary>How many people follow a club, and where that number is going.</summary>
public class CrowdDto
{
    public Guid TeamId { get; init; }

    /// <summary>How many the club ended the season with.</summary>
    public int Supporters { get; init; }

    /// <summary>How many it started with, so a screen can print a delta without dividing two rows.</summary>
    public int OpeningSupporters { get; init; }

    /// <summary>The most anyone held on to it during the season.</summary>
    public int PeakSupporters { get; init; }

    /// <summary>
    /// Whether the following went up over the season. A missing season is not a zero and a club
    /// that lost supporters is not the same fact as a club that has none.
    /// </summary>
    public bool Grew { get; init; }

    public int Change { get; init; }

    public Guid? SeasonId { get; init; }
    public string? SeasonName { get; init; }

    /// <summary>
    /// Null when the club has not played this season, which is different from a club that has
    /// played and drawn nobody.
    /// </summary>
    public double? AverageAttendance { get; init; }

    /// <summary>How full the ground is on average, and how often it turns people away.</summary>
    public CrowdPressureDto? Pressure { get; init; }

    public static CrowdDto From(CrowdView view) => new()
    {
        TeamId = view.TeamId,
        Supporters = view.Supporters,
        OpeningSupporters = view.OpeningSupporters,
        PeakSupporters = view.PeakSupporters,
        Grew = view.Grew,
        Change = view.Change,
        SeasonId = view.SeasonId,
        SeasonName = view.SeasonName,
        AverageAttendance = view.AverageAttendance,
        Pressure = view.Pressure is null ? null : CrowdPressureDto.From(view.Pressure)
    };
}

/// <summary>
/// What the crowd wants against what the ground can hold.
///
/// <para>
/// This is the pair of numbers the whole expansion system rests on, and both of them are here
/// because one of them alone lies. Attendance is people in seats and is never above a hundred
/// per cent of capacity; demand is how many wanted to come and is routinely above it. A club
/// whose ground says "full" and whose demand says "half again as many" is a club with an
/// argument for ten thousand seats, and only the second number says so.
/// </para>
/// </summary>
public class CrowdPressureDto
{
    /// <summary>What the ground holds.</summary>
    public int Capacity { get; init; }

    /// <summary>How many people came, on a season average.</summary>
    public double AverageAttendance { get; init; }

    /// <summary>
    /// How many wanted to come, on the same average, with no ceiling applied. Null when the
    /// club's matches in the season were played before this was kept, which is a club whose
    /// pressure nobody measured and not a club nobody wants to see.
    /// </summary>
    public double? AverageDemand { get; init; }

    /// <summary>
    /// Whether the demand above was actually measured. A screen needs this to tell "no pressure"
    /// apart from "not known yet", and it is on the wire rather than inferred because a null
    /// and a zero are different facts on every field of this contract.
    /// </summary>
    public bool DemandIsMeasured { get; init; }

    /// <summary>How full the ground is. Never above one.</summary>
    public double Occupancy { get; init; }

    /// <summary>
    /// How much of the crowd the ground cannot hold. Null when the pressure was never measured,
    /// which is not the same as zero.
    /// </summary>
    public double? TurnedAwayShare { get; init; }

    /// <summary>Whether the club is being asked for more than it built.</summary>
    public bool Oversubscribed { get; init; }

    /// <summary>
    /// The next project up, with what it costs and how many rounds it takes — null when the
    /// ground is already the biggest one the catalogue can build.
    /// </summary>
    public StadiumProjectDto? NextProject { get; init; }

    public static CrowdPressureDto From(CrowdPressure pressure) => new()
    {
        Capacity = pressure.Capacity,
        AverageAttendance = pressure.AverageAttendance,
        AverageDemand = pressure.AverageDemand,
        DemandIsMeasured = pressure.DemandIsMeasured,
        Occupancy = pressure.Occupancy,
        TurnedAwayShare = pressure.TurnedAwayShare,
        Oversubscribed = pressure.Oversubscribed,
        NextProject = pressure.NextProject is { } project
            ? StadiumProjectDto.From(project)
            : null
    };
}

/// <summary>One of the three things a club can build, and what it costs.</summary>
public class StadiumProjectDto
{
    /// <summary>What the ground has afterwards.</summary>
    public int Seats { get; init; }

    public decimal Cost { get; init; }

    /// <summary>How many matchdays it takes.</summary>
    public int Rounds { get; init; }

    public static StadiumProjectDto From(StadiumProject project) => new()
    {
        Seats = project.Seats,
        Cost = project.Cost,
        Rounds = project.Rounds
    };
}

/// <summary>Building work on a ground, and where it has got to.</summary>
public class StadiumWorkDto
{
    public Guid ConstructionId { get; init; }

    /// <summary>What the ground has afterwards, not what it has today.</summary>
    public int Seats { get; init; }

    public decimal Cost { get; init; }

    /// <summary>How many matchdays it takes.</summary>
    public int Rounds { get; init; }

    /// <summary>Which matchday the work began, so a screen can say how long is left.</summary>
    public int StartedAfterRound { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public static StadiumWorkDto From(StadiumWorkView work) => new()
    {
        ConstructionId = work.ConstructionId,
        Seats = work.Seats,
        Cost = work.Cost,
        Rounds = work.Rounds,
        StartedAfterRound = work.StartedAfterRound,
        StartedAt = work.StartedAt
    };
}

/// <summary>
/// The club's supporters, its ground, its building site and its rivals, in one answer.
///
/// <para>
/// One call because the screen is one page. Four reads taken separately is four requests in
/// flight at once, and a page that renders each as it lands shows a manager a crowd from one
/// year beside a ground from another.
/// </para>
/// </summary>
public class CrowdModuleDto
{
    public CrowdDto Crowd { get; init; } = new();
    public StadiumDto Stadium { get; init; } = new();
    public IReadOnlyList<RivalDto> Rivals { get; init; } = Array.Empty<RivalDto>();

    /// <summary>
    /// The three projects a club can choose between, most seats first. A manager is picking one
    /// of three and not composing one out of five numbers.
    /// </summary>
    public IReadOnlyList<StadiumProjectDto> Catalogue { get; init; } = Array.Empty<StadiumProjectDto>();
}
