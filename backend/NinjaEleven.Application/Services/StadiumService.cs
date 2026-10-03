using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>What came of a club asking to build.</summary>
public enum StadiumWorkKind
{
    /// <summary>Nothing was started: the club is already building, or its ground is as big as this game builds.</summary>
    NothingToBuild = 0,

    /// <summary>A project was started and the club was charged for it.</summary>
    Started = 1
}

/// <summary>
/// A club started building its ground.
/// <param name="Kind">Whether anything was started.</param>
/// <param name="Project">The project, when one was.</param>
/// <param name="Seats">How many seats the club will have when the work is done.</param>
/// <param name="Cost">What it was charged.</param>
/// <param name="FinishesAfterRound">The round at which the seats arrive.</param>
public record StadiumWorkStarted(
    StadiumWorkKind Kind,
    StadiumProject? Project,
    int Seats,
    decimal Cost,
    int FinishesAfterRound);

/// <summary>
/// A ground that finished being built.
/// <param name="StadiumId">The ground.</param>
/// <param name="ClubId">The club that owns it.</param>
/// <param name="SeatsAdded">How many seats arrived.</param>
/// <param name="Capacity">What the ground holds now.</param>
public record StadiumWorkFinished(
    Guid StadiumId,
    Guid ClubId,
    int SeatsAdded,
    int Capacity);

/// <summary>
/// Starting and finishing a club's ground work.
///
/// <para>
/// Two halves and one rule between them. Starting is a manager's decision and is charged to the
/// book in the same unit of work that writes the project, so a club can never be holding a
/// building site it did not pay for. Finishing is nobody's decision at all: it is the world
/// noticing that the rounds the work took have been played, and it is idempotent because the
/// world closes its windows more than once.
/// </para>
///
/// <para>
/// The order matters and is the reason the seats are not added when the money is taken. A club
/// that paid for ten thousand seats gets a ground of five thousand for two rounds, and the
/// crowd in those two rounds is a ground under construction — smaller, because a quarter of the
/// stand is a building site, and not because the expansion has been forgotten. A ground that
/// grew on the day it was paid for would make the project's length a decoration.
/// </para>
/// </summary>
public class StadiumService
{
    private readonly IStadiumConstructionRepository _constructions;
    private readonly IStadiumRepository _stadiums;
    private readonly ITeamRepository _teams;
    private readonly ISeasonRepository _seasons;
    private readonly FinanceService _finance;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StadiumService(
        IStadiumConstructionRepository constructions,
        IStadiumRepository stadiums,
        ITeamRepository teams,
        ISeasonRepository seasons,
        FinanceService finance,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _constructions = constructions;
        _stadiums = stadiums;
        _teams = teams;
        _seasons = seasons;
        _finance = finance;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Starts a project that takes a ground to at least <paramref name="wantedSeats"/>.
    ///
    /// <para>
    /// "At least" rather than "exactly", because the catalogue is three projects and a club
    /// asking for thirty-nine thousand out of a ground of five thousand is asking for something
    /// the catalogue cannot build. Being handed the next project up is the answer a manager can
    /// act on; being told thirty-nine thousand is not a number the game has is not.
    /// </para>
    ///
    /// <para>
    /// A ground that is already building is left alone rather than refused with an error. The
    /// unique index on the open project is the guard that matters — two callers racing here
    /// would otherwise both read "no open project" and both write one — and this is the friendly
    /// reading of the same fact, so a double click costs nothing.
    /// </para>
    /// </summary>
    public async Task<StadiumWorkStarted> StartExpansionAsync(
        Guid teamId,
        int wantedSeats,
        int playedThroughRound,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken);

        if (team?.Stadium is null)
        {
            throw new DomainValidationException("StadiumNotFound", "This club has no ground to build on.");
        }

        var capacity = team.Stadium.Capacity;

        var open = await _constructions.FindOpenForStadiumAsync(team.Stadium.Id, cancellationToken);

        if (open is not null)
        {
            return new StadiumWorkStarted(
                StadiumWorkKind.NothingToBuild,
                StadiumRules.ProjectOf(open.Seats),
                capacity + open.Seats,
                open.Cost,
                open.FinishesAfterRound);
        }

        var project = StadiumRules.ProjectToReach(capacity, wantedSeats);

        if (project is null)
        {
            return new StadiumWorkStarted(
                StadiumWorkKind.NothingToBuild, null, capacity, 0m, playedThroughRound);
        }

        var season = await TheSeasonTheWorldIsInAsync(cancellationToken);

        var construction = StadiumConstruction.Create(
            team.Id,
            team.Stadium.Id,
            season.Id,
            project.Value,
            playedThroughRound,
            _clock.UtcNow);

        await _constructions.AddAsync(construction, cancellationToken);

        await _finance.StageMovementAsync(
            team.Id,
            season.Id,
            matchDayNumber: null,
            FinanceMovementKind.Infrastructure,
            $"Obra no estádio: {project.Value.Seats:N0} lugares",
            -project.Value.Cost,
            matchId: null,
            cancellationToken,
            reference: $"stadium-works:{construction.Id}");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StadiumWorkStarted(
            StadiumWorkKind.Started,
            project,
            capacity + project.Value.Seats,
            project.Value.Cost,
            construction.FinishesAfterRound);
    }

    /// <summary>
    /// Adds the seats of every project whose rounds have been played.
    ///
    /// <para>
    /// It is called from where a window of football closes, and it may be called again for a
    /// window somebody else already closed — so every project answers for itself whether this
    /// call is the one that finished it. A ground whose seats were already added is not counted
    /// a second time, and the report says so rather than claiming a build that did not happen.
    /// </para>
    ///
    /// <para>
    /// The whole world's open projects and the grounds they belong to are read as two sets. The
    /// obvious version of this method asks the database about one ground at a time, and a
    /// matchday that closes four works then costs four round trips to find out which four they
    /// were — on a table that has a row per club per season behind it.
    /// </para>
    /// </summary>
    /// <param name="playedThroughRound">
    /// The highest round number that has been played. A window closing on the third matchday of
    /// a competition season has played through round three.
    /// </param>
    public async Task<IReadOnlyList<StadiumWorkFinished>> SettleFinishedWorksAsync(
        int playedThroughRound,
        CancellationToken cancellationToken = default)
    {
        var open = (await _constructions.ListUnfinishedAsync(cancellationToken))
            .Where(construction => construction.IsDue(playedThroughRound))
            .ToList();

        if (open.Count == 0)
        {
            return Array.Empty<StadiumWorkFinished>();
        }

        var grounds = await _stadiums.ListForUpdateAsync(
            open.Select(construction => construction.StadiumId),
            cancellationToken);

        var byId = grounds.ToDictionary(stadium => stadium.Id);
        var now = _clock.UtcNow;
        var finished = new List<StadiumWorkFinished>();

        foreach (var construction in open)
        {
            // The row's own answer decides, so a settlement that runs twice reports the second
            // run's work as nothing rather than adding the seats to a ground twice.
            if (!construction.Complete(now)) continue;

            _constructions.Update(construction);

            if (!byId.TryGetValue(construction.StadiumId, out var stadium)) continue;

            stadium.SetCapacity(stadium.Capacity + construction.Seats);

            finished.Add(new StadiumWorkFinished(
                stadium.Id,
                construction.ClubId,
                construction.Seats,
                stadium.Capacity));
        }

        if (finished.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return finished;
    }

    /// <summary>
    /// The season a ground is paid for in, which is the one the world is in.
    /// </summary>
    /// <remarks>
    /// Read through the calendar's own question rather than through a second one of this
    /// service's own: <c>GET /season/current</c> already answers "which season is this" for
    /// every other part of the game, and a service that worked it out from the list would be a
    /// fourth answer to a question the world already has.
    /// </remarks>
    private async Task<Season> TheSeasonTheWorldIsInAsync(CancellationToken cancellationToken)
    {
        var current = await _seasons.GetCurrentAsync(cancellationToken);

        if (current is null)
        {
            throw new DomainValidationException(
                "SeasonRequired", "There is no season in progress to build a ground in.");
        }

        return current;
    }
}