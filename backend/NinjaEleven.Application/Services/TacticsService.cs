using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// The board a manager reads before his club kicks off, and the order he leaves on it.
///
/// <para>
/// The screen is one question — who plays, and in what shape — and answering it needs the
/// squad, the opponent, the last few results and what has happened between these two clubs
/// before. Those are five reads of five different tables, and they are read here in one
/// service because the screen asking for them one at a time is five round trips and a set of
/// panels that fill in one after another on a screen whose whole job is to be read at a
/// glance.
/// </para>
///
/// <para>
/// The plan is stored per club and per season rather than per fixture. A plan is how this
/// club plays; the fixture it lands on is read from the calendar when the match starts, which
/// is the only place a fixture exists. A screen that asked the manager to re-say his eleven
/// every eight days would be a manager managing a setting rather than a team.
/// </para>
/// </summary>
public class TacticsService
{
    /// <summary>
    /// How many finished matches of the club's past the board shows.
    ///
    /// Five is a form rather than a history: enough to see a run and a dip, few enough that
    /// the last thing that happened is still on the screen when the manager looks.
    /// </summary>
    private const int RecentFormMatches = 5;

    private readonly ITeamMatchPlanRepository _plans;
    private readonly ITeamRepository _teams;
    private readonly IFixtureRepository _fixtures;
    private readonly IMatchRepository _matches;
    private readonly TeamService _squads;
    private readonly MatchdayService _matchday;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public TacticsService(
        ITeamMatchPlanRepository plans,
        ITeamRepository teams,
        IFixtureRepository fixtures,
        IMatchRepository matches,
        TeamService squads,
        MatchdayService matchday,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _plans = plans;
        _teams = teams;
        _fixtures = fixtures;
        _matches = matches;
        _squads = squads;
        _matchday = matchday;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Everything the board is made of, in one call.
    ///
    /// The reads do not wait for each other: they are independent questions about the world
    /// and there is no order among them, so they are started together and joined once. The
    /// opponent is the exception that proves the rule — it cannot be asked before the next
    /// fixture is known, which is why the read after this one is the only one that is asked
    /// second and not first.
    /// </summary>
    public async Task<TacticsBoard> GetBoardAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        // Read one after another, and not four at once.
        //
        // <para>
        // Four repositories over four queries is four queries' worth of latency, and running
        // them together would be four queries' worth of wall clock — which is exactly what
        // <c>Task.WhenAll</c> was for until the day it threw. The repositories share one
        // context, a context runs one operation at a time, and asking it for four at once is
        // not four reads in parallel: it is a concurrency detector that refuses the second
        // one. The board is read in sequence, and a set is still read as a set — the fix for
        // a read that costs a query per row is a reader that takes the whole row, never a
        // second thread.
        // </para>
        var squad = await _squads.GetSquadAsync(teamId, seasonId, cancellationToken);
        var next = await _matchday.GetNextFixtureWindowAsync(teamId, seasonId, cancellationToken);
        var plan = await _plans.GetAsync(teamId, seasonId, cancellationToken);
        var recent = await _matches.GetTeamHistoryAsync(teamId, RecentFormMatches, cancellationToken);

        var opponent = next is null ? null : await ReadTheOpponentAsync(teamId, next.FixtureId, cancellationToken);

        var headToHead = opponent is null
            ? null
            : await _matches.GetHeadToHeadSummaryAsync(teamId, opponent.Id, cancellationToken);

        return new TacticsBoard
        {
            TeamId = team.Id,
            TeamName = team.Name,
            SeasonId = seasonId,
            Next = next,
            Opponent = opponent,
            HeadToHead = headToHead,
            RecentForm = recent,
            Squad = squad
                .Select(TacticsSquadRow.From)
                .OrderBy(row => PositionOrder.Of(row.Position))
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Plan = plan is null ? null : new TacticsPlan(
                plan.TacticCode,
                plan.StarterIds,
                plan.BenchIds,
                plan.UpdatedAt)
        };
    }

    /// <summary>
    /// The club on the other side of the next fixture, and whether this match is at home.
    ///
    /// It is asked of the fixture rather than of the round, because the round says which two
    /// clubs are playing somewhere and the fixture says which of them is at home — and a
    /// manager picking a plan for a game he thinks is away is a plan for the wrong pitch.
    /// </summary>
    private async Task<TacticsOpponent?> ReadTheOpponentAsync(
        Guid teamId,
        Guid fixtureId,
        CancellationToken cancellationToken)
    {
        var fixture = await _fixtures.GetAsync(fixtureId, cancellationToken);
        if (fixture is null)
        {
            return null;
        }

        var atHome = fixture.HomeTeamId == teamId;
        var opponentId = atHome ? fixture.AwayTeamId : fixture.HomeTeamId;

        var opponent = await _teams.GetAsync(opponentId, cancellationToken);
        if (opponent is null)
        {
            return null;
        }

        return new TacticsOpponent(opponentId, opponent.Name, opponent.Rating, atHome);
    }

    /// <summary>
    /// Records the order, replacing whatever this club had planned for this season.
    ///
    /// The order is checked against the squad as it is now, and a man who is out is refused
    /// rather than quietly dropped: a plan that silently loses two players is a plan the
    /// manager will read on the day as the eleven he chose, and it was not.
    /// </summary>
    public async Task<TacticsPlan> SavePlanAsync(
        Guid teamId,
        Guid seasonId,
        string? tacticCode,
        IReadOnlyList<Guid> starterIds,
        IReadOnlyList<Guid> benchIds,
        CancellationToken cancellationToken = default)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        // The squad is read for the order to be checked against, and the check is against
        // this club's season rather than a global list: a player id from another club is not
        // "unavailable", it is not a player this manager may name.
        var squad = await _squads.GetSquadAsync(teamId, seasonId, cancellationToken);
        var byId = squad.ToDictionary(row => row.Player.Id);

        var code = (tacticCode ?? string.Empty).Trim();

        if (!string.IsNullOrEmpty(code) && Tactics.Find(code) is null)
        {
            throw new DomainValidationException(
                "UnknownTactic",
                $"Tática desconhecida: '{code}'. Use um código de {string.Join(", ", Tactics.All.Select(option => option.Code))}.");
        }

        var starters = starterIds.Distinct().ToList();
        var bench = benchIds.Distinct().ToList();

        // A man cannot be in the eleven and on the bench. The overlap is refused rather than
        // de-duplicated, because a screen that sent the same id twice has a bug and the
        // manager deserves to be told his order was not understood rather than given one that
        // quietly means something else.
        var both = starters.Intersect(bench).ToList();
        if (both.Count > 0)
        {
            throw new DomainValidationException(
                "PlayerAlreadySelected",
                $"O jogador {both[0]} não pode estar entre os titulares e entre os reservas ao mesmo tempo.");
        }

        foreach (var playerId in starters.Concat(bench))
        {
            if (!byId.TryGetValue(playerId, out var row))
            {
                throw new DomainValidationException(
                    "PlayerNotInSquad",
                    $"O jogador {playerId} não pertence ao elenco do {team.Name}.");
            }

            if (!row.SeasonState.IsAvailable)
            {
                throw new DomainValidationException(
                    "PlayerNotAvailable",
                    $"{row.Player.Name} não está à disposição para esta partida.");
            }
        }

        if (starters.Count > 0 && starters.Count != MatchRules.SquadSize)
        {
            throw new DomainValidationException(
                "IncompleteEleven",
                $"Um time titular tem {MatchRules.SquadSize} jogadores; foram nomeados {starters.Count}.");
        }

        if (starters.Count > 0)
        {
            // Exactly one, not at least one. A team sheet with two names in the goalkeeper band
            // is not a plan that will be adjusted on the day: it is two goalkeepers on the
            // pitch, because a replacement for one of them comes from the line the man plays and
            // a keeper's place has nobody to replace it from. Refusing it here is the only
            // point at which anybody is still there to be told.
            var keepers = starters.Count(id => byId[id].Player.Position == Position.GK);

            if (keepers != 1)
            {
                throw new DomainValidationException(
                    "GoalkeeperRequired",
                    $"Um time titular tem exatamente um goleiro; foram nomeados {keepers}.");
            }
        }

        var now = _clock.UtcNow;
        var existing = await _plans.GetAsync(teamId, seasonId, cancellationToken);

        // The plan is one row per club and season, so a restatement is the row the context
        // already holds rather than a second answer to "how does this club play".
        if (existing is not null)
        {
            existing.Restate(code, starters, bench, now);
            _plans.Update(existing);
        }
        else
        {
            await _plans.AddAsync(TeamMatchPlan.Create(teamId, seasonId, code, starters, bench, now));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TacticsPlan(code, starters, bench, now);
    }

    /// <summary>
    /// The order a club kicks off with when nobody is there to give one.
    ///
    /// <para>
    /// Three answers, in order of how much they mean. The plan the manager left is the first,
    /// because he wrote it for this club and this season. The shape his club last used on the
    /// pitch is the second, because a manager who said nothing this week still has a habit.
    /// The shape read off the squad is the last, and it is the answer the engine would have
    /// produced on its own, so a club nobody has spoken for arrives at the pitch playing the
    /// way it is built.
    /// </para>
    ///
    /// <para>
    /// It returns an order and not the row it came from, because the second answer is not a
    /// row at all: a shape the club once used is a column on a finished match, and a plan
    /// assembled out of a plan and a column is the kick-off's business rather than the
    /// repository's.
    /// </para>
    /// </summary>
    public async Task<KickOffPlan?> TheOrderAtTheKickOffAsync(
        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var planned = await _plans.GetAsync(teamId, seasonId, cancellationToken);

        if (planned is not null && planned.NamesAnEleven)
        {
            return new KickOffPlan(planned.TacticCode, planned.StarterIds, planned.BenchIds);
        }

        // No eleven was named, so the shape is what the club last put on the pitch with. It
        // is asked of the club's own matches rather than of the season's calendar, so a club
        // whose last game was in the cup is not told it played 4-4-2 because that is what it
        // did in the league.
        var lastShape = planned?.TacticCode;
        if (string.IsNullOrWhiteSpace(lastShape))
        {
            lastShape = await _matches.GetLastTacticCodeAsync(teamId, cancellationToken);
        }

        return new KickOffPlan(lastShape ?? string.Empty, [], []);
    }
}

/// <summary>What the board is made of.</summary>
public sealed class TacticsBoard
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid SeasonId { get; init; }

    /// <summary>The club's next fixture, or null when the season has nothing left to play.</summary>
    public NextFixtureWindow? Next { get; init; }

    public TacticsOpponent? Opponent { get; init; }
    public HeadToHeadSummary? HeadToHead { get; init; }
    public IReadOnlyList<TeamMatchRecord> RecentForm { get; init; } = [];
    public IReadOnlyList<TacticsSquadRow> Squad { get; init; } = [];
    public TacticsPlan? Plan { get; init; }
}

/// <summary>
/// The club on the other side, and which of the two pitches is being played on.
///
/// <para>
/// The flag is about the club the board belongs to and not about the opponent, because the
/// screen's two names are a fixture: swapping them puts this club on the wrong side of its
/// own match. It is named for the holder rather than left as <c>IsHome</c>, which reads as
/// the other club's answer and is the opposite of the one the screen needs.
/// </para>
/// </summary>
public sealed record TacticsOpponent(Guid Id, string Name, int Rating, bool ClubIsAtHome);

/// <summary>One man of the squad, as far as a board needs him.</summary>
public sealed record TacticsSquadRow(
    Guid PlayerId,
    string Name,
    int Age,
    Position Position,
    double Stars,
    int Energy,
    bool IsAvailable,
    IReadOnlyList<int> Attributes,
    int SuspensionMatches,
    int InjuryMatchesRemaining)
{
    /// <summary>
    /// The row the board shows, read off the squad the club screen already reads.
    ///
    /// <para>
    /// It is the same squad the manager has been looking at all week rather than a second
    /// reading of the same table: a board whose squad disagreed with the club's own page would
    /// be two answers to "who is available", and only one of them would be the one the kick-off
    /// uses.
    /// </para>
    ///
    /// <para>
    /// The eight attributes travel with the row because the decision on this screen is made by
    /// comparing men, and a manager can only compare men he can see. Two rows that both said
    /// "3.5 estrelas, 82 de energia" leave the choice to be made by name, which is the one
    /// thing he does not know and the whole reason for having a board.
    /// </para>
    ///
    /// <para>
    /// They are in the catalogue's own order rather than sorted, so the tag strip reads left
    /// to right as the same thing it reads on the squad table and on the training sheet.
    /// </para>
    ///
    /// <para>
    /// The two absence counters travel with the row because a board that greyed a man out
    /// without saying why would make the manager open the club's own page to find out — the one
    /// place the answer already is. <c>IsAvailable</c> says he cannot play; these say whether
    /// it is two matches of a suspension or a knock, which is the difference between a plan
    /// that will be fine in a fortnight and one that needs somebody else.
    /// </para>
    /// </summary>
    public static TacticsSquadRow From(SquadPlayer squad) => new(
        squad.Player.Id,
        squad.Player.Name,
        squad.Player.Age,
        squad.Player.Position,
        PlayerRating.CalculateStars(squad.Player),
        squad.SeasonState.Energy,
        squad.SeasonState.IsAvailable,
        AttributesInOrder.Select(squad.Player.Get).ToList(),
        squad.SeasonState.SuspensionMatches,
        squad.SeasonState.InjuryMatchesRemaining);

    /// <summary>
    /// The eight, in the order every other table in the game reads them.
    ///
    /// <para>
    /// Stamina is last rather than hidden because it is the one number that decides whether
    /// the other seven are available on Saturday: a man on four who is technically the best
    /// midfielder in the squad is a man who comes off at sixty. It is carried and not
    /// computed, because the board is a reading of the man rather than a second opinion about
    /// how long he will last.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<PlayerAttribute> AttributesInOrder =
    [
        PlayerAttribute.Speed,
        PlayerAttribute.Accuracy,
        PlayerAttribute.Dribbling,
        PlayerAttribute.Heading,
        PlayerAttribute.Strength,
        PlayerAttribute.GoalkeeperPower,
        PlayerAttribute.Reflexes,
        PlayerAttribute.Stamina
    ];
}

/// <summary>The standing order, as it was left.</summary>
public sealed record TacticsPlan(
    string TacticCode,
    IReadOnlyList<Guid> StarterIds,
    IReadOnlyList<Guid> BenchIds,
    DateTimeOffset UpdatedAt);

/// <summary>The order a kick-off actually goes out with.</summary>
public sealed record KickOffPlan(
    string TacticCode,
    IReadOnlyList<Guid> StarterIds,
    IReadOnlyList<Guid> BenchIds);
