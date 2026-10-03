using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>The four pieces of the module, read together.</summary>
public class CrowdModule
{
    public CrowdView Crowd { get; init; } = new();
    public StadiumView? Stadium { get; init; }
    public IReadOnlyList<RivalLine> Rivals { get; init; } = Array.Empty<RivalLine>();
}

/// <summary>
/// The whole crowd module of a club, in one answer: how many follow it, how big its ground is,
/// what is being built on it, and who it is a rival of.
///
/// <para>
/// One call because the screen is one page. Four reads taken separately is four requests in
/// flight at once, and a page that draws each as it lands shows a manager a crowd from one year
/// next to a ground from another — which is the same mistake the club's own page had.
/// </para>
/// </summary>
public class CrowdService
{
    private readonly ITeamRepository _teams;
    private readonly ITeamFanBaseRepository _fanBases;
    private readonly IStadiumRepository _stadiums;
    private readonly IStadiumConstructionRepository _constructions;
    private readonly IMatchRepository _matches;
    private readonly ISeasonRepository _seasons;
    private readonly RivalryService _rivalries;

    public CrowdService(
        ITeamRepository teams,
        ITeamFanBaseRepository fanBases,
        IStadiumRepository stadiums,
        IStadiumConstructionRepository constructions,
        IMatchRepository matches,
        ISeasonRepository seasons,
        RivalryService rivalries)
    {
        _teams = teams;
        _fanBases = fanBases;
        _stadiums = stadiums;
        _constructions = constructions;
        _matches = matches;
        _seasons = seasons;
        _rivalries = rivalries;
    }

    /// <summary>
    /// Everything about one club's supporters, in one read of the club and four small ones.
    /// </summary>
    public async Task<CrowdView?> CrowdOfAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken);

        if (team is null)
        {
            return null;
        }

        var season = await _seasons.GetCurrentAsync(cancellationToken);

        var fanBase = season is null
            ? null
            : await _fanBases.GetAsync(teamId, season.Id, cancellationToken);

        var grounds = await _stadiums.ListAsync(cancellationToken);
        var ground = grounds.FirstOrDefault(candidate => candidate.ClubId == teamId);

        // What came through the turnstiles and what wanted to, for this season. Both are read
        // from the matches themselves rather than worked out from the club's following: a
        // reconstruction has to invent the table position and the matchday, and an invented
        // number printed as the engine's is the one thing this page must not do.
        HomeGateReading? gate = null;

        if (season is not null)
        {
            var gates = await _matches.ReadHomeGatesAsync(
                season.Id,
                new[] { teamId },
                cancellationToken);

            gate = gates.GetValueOrDefault(teamId);
        }

        var pressure = ground is null || gate is null
            ? null
            : new CrowdPressure
            {
                Capacity = ground.Capacity,
                AverageAttendance = gate.AverageAttendance,
                AverageDemand = gate.AverageDemand ?? 0,
                DemandIsMeasured = gate.AverageDemand is not null,
                NextProject = StadiumRules.NextProjectFor(ground.Capacity)
            };

        return new CrowdView
        {
            TeamId = teamId,
            Supporters = fanBase?.Supporters ?? 0,
            OpeningSupporters = fanBase?.OpeningSupporters ?? 0,
            PeakSupporters = fanBase?.PeakSupporters ?? 0,
            Grew = fanBase?.Grew ?? false,
            Change = fanBase?.Change ?? 0,
            SeasonId = season?.Id,
            SeasonName = season?.Name,
            AverageAttendance = gate?.HomeMatches > 0 ? gate.AverageAttendance : null,
            Pressure = pressure
        };
    }

    /// <summary>
    /// A ground, and the project on it if there is one.
    /// </summary>
    public async Task<StadiumView?> StadiumOfAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var ground = (await _stadiums.ListAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.ClubId == teamId);

        if (ground is null)
        {
            return null;
        }

        // Only the open project, and only for this ground. The reader answers "is this one being
        // worked on" so a page does not have to hold every building site in the country to draw
        // one club's.
        var work = await _constructions.FindOpenForStadiumAsync(ground.Id, cancellationToken);

        return new StadiumView
        {
            ClubId = teamId,
            Name = ground.Name,
            Capacity = ground.Capacity,
            TicketPrice = ground.TicketPrice,
            Work = work is null
                ? null
                : new StadiumWorkView
                {
                    ConstructionId = work.Id,
                    Seats = work.Seats,
                    Cost = work.Cost,
                    Rounds = work.Rounds,
                    StartedAfterRound = work.StartedAfterRound,
                    StartedAt = work.StartedAt
                }
        };
    }

    /// <summary>
    /// The whole module in one answer, and null when the club is not there.
    ///
    /// <para>
    /// The controller answers a missing club with a code rather than an empty page: a club that
    /// does not exist and a club with no crowd yet are different answers, and collapsing them
    /// would make a manager look for a crowd that was never going to be written.
    /// </para>
    /// </summary>
    public async Task<CrowdModule> ModuleOfAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var crowd = await CrowdOfAsync(teamId, cancellationToken);

        if (crowd is null)
        {
            return null;
        }

        var stadium = await StadiumOfAsync(teamId, cancellationToken);

        return new CrowdModule
        {
            Crowd = crowd,
            Stadium = stadium,
            Rivals = await RivalsOfAsync(teamId, cancellationToken)
        };
    }

    /// <summary>The four clubs this one is most a rival of.</summary>
    public Task<IReadOnlyList<RivalLine>> RivalsOfAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        _rivalries.RivalsOfAsync(teamId, cancellationToken);

    /// <summary>
    /// The three projects on offer, most seats first. A manager is choosing one of three, and a
    /// screen that listed the whole curve of every possible stand would be a screen asking a
    /// question the catalogue cannot answer.
    /// </summary>
    public static IReadOnlyList<StadiumProject> Catalogue() => StadiumRules.Projects;
}
