using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Application.Services;

/// <summary>
/// What closing a season did, so a caller can be told rather than left to look.
/// </summary>
/// <param name="SeasonId">The season that was closed.</param>
/// <param name="NextSeasonId">The season that was opened after it, when one was.</param>
/// <param name="PrizesPaid">How many clubs were paid a championship purse.</param>
/// <param name="Champions">The champion of each tier, top tier first.</param>
public record SeasonCloseResult(
    Guid SeasonId,
    Guid? NextSeasonId,
    int PrizesPaid,
    IReadOnlyList<Guid> Champions);

/// <summary>
/// Closes a season, pays what is owed, and opens the next one.
///
/// **Closing a season is three things that must happen together.** The tables have to stop
/// moving, the money has to be paid, and the pyramid has to be rearranged — because a season
/// that pays its prizes but leaves every club in the division it was promoted out of is a
/// season whose last day and whose first day of the next one disagree, and the second of
/// those is the one a manager has to play in.
///
/// **The prize is the division's purse, split by the weight of a position.** Every club in a
/// division is paid, the champion takes a great deal of it and the twelfth place takes a
/// little, and the four going down are paid exactly like the four staying up: a relegated club
/// has been in the competition for thirty matchdays and a prize that stopped at the
/// relegation line would be a prize for staying, which is a different competition.
///
/// **The trophies are written, not recomputed.** A club that is relegated after winning still
/// won, and a shelf that read the current division of a club to decide what it has won would
/// take the 1ª Divisão title away from a club that is now in the 2ª.
///
/// **The artilharia is paid here too, and it is money on top.** The three top scorers of every
/// division and of the cup are paid a share of the champion's own prize, and that money is not
/// taken off anybody: a title is paid for a season's football and a prize for scoring is paid
/// for a season's goals, so a club that wins the division and whose striker wins the artilharia
/// is paid twice. A club may take both prizes, and two of its own men may be in the same
/// artilharia — nothing says a club's goals are worth less because they were scored for it.
///
/// The Supercup is not on the list. It is one match between two clubs, and an artilharia of one
/// evening is a striker's single goal being paid a season's first prize; a prize nobody would
/// believe if they read it is better not to exist.
///
/// **The Supercup is the first match of the season that follows**, between the champion of
/// the first division and the cup's winner, and a club that won both is not allowed to play
/// itself: the second club of the first division comes in its place, because a Supercup is two
/// clubs meeting and the point of it is a new season's first trophy, not a rematch of the last
/// season's two trophies by the same club twice.
/// </summary>
public class SeasonCloseService : ISeasonCloser
{
    private readonly ISeasonRepository _seasons;
    private readonly ICompetitionRepository _competitions;
    private readonly IDivisionRepository _divisions;
    private readonly IRoundRepository _rounds;
    private readonly IMatchDayRepository _matchDays;
    private readonly IFixtureRepository _fixtures;
    private readonly ITrophyRepository _trophies;
    private readonly ITeamRepository _teams;
    private readonly StandingsService _standings;
    private readonly SeasonCalendarService _calendar;
    private readonly FinanceService _finance;
    private readonly IDataSeeder _seeder;
    private readonly RosterService _roster;
    private readonly TransferService _transfers;
    private readonly InboxService _inbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeasonCloseService> _logger;

    public SeasonCloseService(
        ISeasonRepository seasons,
        ICompetitionRepository competitions,
        IDivisionRepository divisions,
        IRoundRepository rounds,
        IMatchDayRepository matchDays,
        IFixtureRepository fixtures,
        ITrophyRepository trophies,
        ITeamRepository teams,
        StandingsService standings,
        SeasonCalendarService calendar,
        FinanceService finance,
        IDataSeeder seeder,
        RosterService roster,
        TransferService transfers,
        InboxService inbox,
        IUnitOfWork unitOfWork,
        ILogger<SeasonCloseService> logger)
    {
        _seasons = seasons;
        _competitions = competitions;
        _divisions = divisions;
        _rounds = rounds;
        _matchDays = matchDays;
        _fixtures = fixtures;
        _trophies = trophies;
        _teams = teams;
        _standings = standings;
        _calendar = calendar;
        _finance = finance;
        _seeder = seeder;
        _roster = roster;
        _transfers = transfers;
        _inbox = inbox;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Whether a season has nothing left to play: every window of every matchday has been
    /// played. It is read from the fixtures rather than from a flag, so a season that was left
    /// half played by a restart is not a season that is over.
    /// </summary>
    public async Task<bool> IsFinishedAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var matchDays = await _matchDays.ListBySeasonAsync(seasonId, cancellationToken);
        if (matchDays.Count == 0)
        {
            return false;
        }

        var dayIds = matchDays.Select(day => day.Id).ToList();
        var rounds = (await _rounds.ListAsync(cancellationToken))
            .Where(round => round.MatchDayId is not null && dayIds.Contains(round.MatchDayId.Value))
            .ToList();

        if (rounds.Count == 0)
        {
            return false;
        }

        var fixtures = await _fixtures.ListByRoundIdsAsync(
            rounds.Select(round => round.Id), cancellationToken);

        return fixtures.Count > 0
            && fixtures.All(fixture => fixture.Status == FixtureStatus.Finished);
    }

    /// <summary>
    /// Pays the championship prizes, writes the trophies and marks the season finished.
    ///
    /// It is safe to call twice: every payment is guarded by what it is the payment for, the
    /// trophies are guarded by the edition they were won in, and a season that is already
    /// finished says so instead of paying again.
    /// </summary>
    public async Task<SeasonCloseResult> CloseAsync(
        Guid seasonId,
        bool openTheNextSeason = true,
        CancellationToken cancellationToken = default)
    {
        var season = await _seasons.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        var views = await _competitions.ListSeasonViewsAsync(seasonId, cancellationToken);
        var divisions = views.Where(view => view.Type == CompetitionType.League && view.Tier is not null).ToList();

        var champions = new List<Guid>();
        var paid = 0;

        // The four tables, read once and kept: the prizes, the trophies and the season's own
        // letter are all the same standings, and a service that asked the standings service for
        // them twice would be paying twice for one fact and could be handed two different
        // answers if a match were settled in between.
        var tables = new Dictionary<int, IReadOnlyList<StandingEntry>>();

        foreach (var view in divisions)
        {
            var collection = await _standings.CollectAsync(view.Id, view, cancellationToken);
            if (collection.Seeds.Count == 0)
            {
                continue;
            }

            var table = StandingTable.Build(collection.Seeds, collection.Finished);
            tables[view.Tier!.Value] = table;
            var clubs = table.Count;

            for (var position = 1; position <= clubs; position++)
            {
                var clubId = table[position - 1].TeamId;
                var prize = PrizeRules.ChampionshipPrize(
                    position, clubs, PrizeRules.PurseForTier(view.Tier!.Value));

                var line = await _finance.RecordPrizeAsync(
                    clubId,
                    seasonId,
                    FinanceMovementKind.PrizeMoney,
                    $"{PositionName(position)} da {CompetitionRules.DivisionName(view.Tier.Value)}",
                    prize,
                    $"championship:{view.Id}:{position}",
                    cancellationToken);

                if (line is not null)
                {
                    paid++;
                }

                // The title is announced, the purse is reported by the ledger on its own, and
                // the two together are what a manager wants to know: that his club is a
                // champion and what the championship cost. Only first place is announced —
                // second and third are podium, and a box that congratulated a club on being
                // third would make a title mean nothing.
                if (position == 1)
                {
                    var championClub = await _teams.GetAsync(clubId, cancellationToken);

                    if (championClub is not null)
                    {
                        await _inbox.PostTitleAsync(
                            new TitleFacts
                            {
                                RecipientTeamId = championClub.Id,
                                ClubName = championClub.Name,
                                Kind = InboxTitleKind.Championship,
                                CompetitionName = CompetitionRules.DivisionName(view.Tier!.Value),
                                SeasonName = season.Name,
                                Position = 1,
                                PrizeMoney = prize,
                                Reference = $"championship:{view.Id}:1"
                            },
                            cancellationToken);
                    }
                }
            }

            var champion = table[0].TeamId;
            champions.Add(champion);
            await AwardThePodiumAsync(view, seasonId, table, cancellationToken);
        }

        season.Finish();
        _seasons.Update(season);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The season's letter goes out before the next season is opened, and after the save, so
        // a message is never written about a close that was then rolled back. It is one message
        // to every manager because a season ends by rearranging the country: the man in the 4ª
        // Divisão watched two clubs leave his table and two arrive, and four letters — one per
        // division, each about somebody else's — would be a season that ended four times over.
        await PostTheSeasonLetterAsync(season, divisions, tables, cancellationToken);

        _logger.LogInformation(
            "Closed season {Number} ({Name}): {Prizes} championship prizes paid, {Champions} champions.",
            season.Number,
            season.Name,
            paid,
            champions.Count);

        Guid? nextSeasonId = null;

        if (openTheNextSeason)
        {
            nextSeasonId = await OpenTheNextSeasonAsync(season, cancellationToken);
        }

        return new SeasonCloseResult(seasonId, nextSeasonId, paid, champions);
    }

    /// <summary>
    /// Writes the season's letter: the four final tables, who moved, and where the reader's own
    /// club finished.
    /// </summary>
    ///
    /// <para>
    /// It is built from the tables the close has already read, and the movement from the same
    /// rule that will rearrange the pyramid an instant later — <see cref="DivisionMovement"/> —
    /// because a letter that said a club stayed while the calendar put it in another division
    /// would be a season ending twice with two different endings.
    /// </para>
    ///
    /// <para>
    /// A division with no table is left out rather than written as empty: a pyramid of three
    /// divisions in a world being seeded is three tables, and a fourth block saying "no clubs"
    /// is a division the game does not have.
    /// </para>
    /// </summary>
    private async Task PostTheSeasonLetterAsync(
        Season season,
        IReadOnlyList<CompetitionSeasonView> divisions,
        IReadOnlyDictionary<int, IReadOnlyList<StandingEntry>> tables,
        CancellationToken cancellationToken)
    {
        if (tables.Count == 0)
        {
            return;
        }

        var clubNames = (await _teams.ListByIdsAsync(
                tables.Values.SelectMany(table => table).Select(entry => entry.TeamId).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(club => club.Id, club => club.Name);

        var summaryDivisions = new List<SeasonSummaryDivision>();

        foreach (var view in divisions.OrderBy(view => view.Tier))
        {
            if (view.Tier is not { } tier || !tables.TryGetValue(tier, out var table) || table.Count == 0)
            {
                continue;
            }

            var movement = DivisionMovement.From(tier, table);
            var byClub = movement.Movements.ToDictionary(club => club.TeamId);

            summaryDivisions.Add(new SeasonSummaryDivision
            {
                Tier = tier,
                DivisionName = CompetitionRules.DivisionName(tier),
                Lines = table.Select(entry => new SeasonSummaryLine
                {
                    TeamId = entry.TeamId,
                    ClubName = clubNames.GetValueOrDefault(entry.TeamId, entry.TeamId.ToString()),
                    Position = entry.Position,
                    Points = entry.Points,
                    Played = entry.Played,
                    Wins = entry.Wins,
                    Draws = entry.Draws,
                    Losses = entry.Losses,
                    GoalsFor = entry.GoalsFor,
                    GoalsAgainst = entry.GoalsAgainst,
                    Movement = !byClub.TryGetValue(entry.TeamId, out var clubMovement)
                        ? SeasonMovementKind.Stays
                        : clubMovement.IsPromoted ? SeasonMovementKind.Promoted
                        : clubMovement.IsRelegated ? SeasonMovementKind.Relegated
                        : SeasonMovementKind.Stays
                }).ToList()
            });
        }

        await _inbox.PostSeasonSummaryAsync(
            new SeasonSummaryFacts
            {
                SeasonName = season.Name,
                SeasonId = season.Id,
                Divisions = summaryDivisions
            },
            cancellationToken);
    }

    /// <summary>
    /// The three places in a division that get a trophy, written and not worked out again.
    /// </summary>
    private async Task AwardThePodiumAsync(
        CompetitionSeasonView view,
        Guid seasonId,
        IReadOnlyList<StandingEntry> table,
        CancellationToken cancellationToken)
    {
        var already = await _trophies.ListBySeasonAsync(seasonId, cancellationToken);
        if (already.Any(trophy => trophy.CompetitionSeasonId == view.Id))
        {
            return;
        }

        var awards = new List<TrophyAward>();
        var kinds = new[] { TrophyKind.Champion, TrophyKind.RunnerUp, TrophyKind.Third };

        for (var position = 1; position <= Math.Min(kinds.Length, table.Count); position++)
        {
            awards.Add(TrophyAward.Create(
                table[position - 1].TeamId,
                seasonId,
                view.Id,
                view.DivisionId,
                kinds[position - 1],
                // The money is in the club's book, in its own line, with its own reference. The
                // trophy carries the figure so a shelf can say what a title was worth.
                PrizeRules.ChampionshipPrize(
                    position,
                    table.Count,
                    PrizeRules.PurseForTier(view.Tier ?? 1))));
        }

        await _trophies.AddRangeAsync(awards, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Opens the season after this one: the pyramid rearranged, the three competitions drawn
    /// for it, the calendar laid out and the money carried across.
    /// </summary>
    private async Task<Guid> OpenTheNextSeasonAsync(
        Season previous,
        CancellationToken cancellationToken)
    {
        if (await FindTheNextSeasonAsync(previous, cancellationToken) is { } existing)
        {
            return existing.Id;
        }

        var competitions = await _competitions.ListAsync(cancellationToken);
        var championship = competitions.FirstOrDefault(c => c.Type == CompetitionType.League)
            ?? throw new DomainValidationException(
                "SeasonHasNoLeague", "O mundo não tem um campeonato para continuar.");
        var cup = competitions.FirstOrDefault(c => c.Type == CompetitionType.Cup);
        var superCup = competitions.FirstOrDefault(c => c.Type == CompetitionType.SuperCup);

        var divisions = (await _divisions.ListAsync(cancellationToken))
            .OrderBy(division => division.Tier)
            .ToList();

        // The new season starts the day after the old one ended, and lasts exactly as long as a
        // season lasts: thirty-four days, the last of which has no football in it. The old
        // season's own end date is not carried over because a season's length is a rule of the
        // calendar and not a property of the season before it — a world whose first season was
        // drawn by hand over a year would otherwise give every season after it a year of days.
        var start = previous.EndDate.AddDays(1);
        var season = Season.Create(
            previous.Number + 1,
            start,
            start.AddDays(CompetitionRules.SeasonMatchDays - 1));
        season.Start();
        await _seasons.AddAsync(season, cancellationToken);

        var movements = await ReadThePyramidAsync(previous, cancellationToken);
        var participants = new List<CompetitionParticipant>();
        var editions = new List<CompetitionSeason>();

        foreach (var division in divisions)
        {
            var edition = CompetitionSeason.Create(championship.Id, season.Id, division.Id);
            editions.Add(edition);

            foreach (var teamId in ClubsOf(division.Tier, movements, divisions))
            {
                participants.Add(CompetitionParticipant.Create(edition.Id, teamId));
            }
        }

        if (cup is not null)
        {
            editions.Add(CompetitionSeason.Create(cup.Id, season.Id));
        }

        if (superCup is not null)
        {
            editions.Add(CompetitionSeason.Create(superCup.Id, season.Id));
        }

        foreach (var edition in editions)
        {
            await _competitions.AddSeasonAsync(edition, cancellationToken);
        }

        await _competitions.AddParticipantsAsync(participants, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The calendar is drawn before the money is carried, because the Supercup's two clubs
        // come from the last season's trophies and the cups' entrants are seeded from the last
        // season's tables — both of which exist by now.
        await _calendar.BuildAsync(season.Id, cancellationToken);

        // And the balances come last, so the first line of the new season's book is the balance
        // the club closed the last one with and not a copy of a book that is still being
        // written.
        await _finance.CarryBalancesIntoSeasonAsync(previous.Id, season.Id, cancellationToken);

        // The rosters follow the money: the men who said they would stop at the end of the old
        // season are free agents now, everybody else is given a season to play, and the deals
        // that were waiting for this season are pointed at it.
        await _roster.OpenSeasonAsync(previous, season, cancellationToken);

        // A proposal nobody answered in the season that has just ended is a rumour, not a deal,
        // and a rumour left alive would go on saying the player is spoken for.
        await _transfers.ExpireUnansweredAsync(previous.Id, cancellationToken);

        // The season opens with its intake: the young academy players a club develops rather
        // than buys. Each club receives up to AcademyRules.MaxAcademyPlayersPerClub youth players,
        // aged 16 to 21, assigned to the club with the academy flag set.
        await _seeder.GenerateAcademyPlayersAsync(season.Id, cancellationToken);

        _logger.LogInformation(
            "Opened season {Number} ({Name}) from {Previous}.",
            season.Number,
            season.Name,
            previous.Name);

        return season.Id;
    }

    private async Task<Season?> FindTheNextSeasonAsync(
        Season previous,
        CancellationToken cancellationToken) =>
        (await _seasons.ListAsync(cancellationToken))
            .FirstOrDefault(season => season.Number == previous.Number + 1);

    /// <summary>
    /// Where every club of the pyramid ends up, read from the tables of the season that is
    /// closing. The movements are worked out by the domain and only read here: a service that
    /// settled each club one at a time would promote a club into a place that the club leaving
    /// it had not yet vacated.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, DivisionMovement>> ReadThePyramidAsync(
        Season previous,
        CancellationToken cancellationToken)
    {
        var views = await _competitions.ListSeasonViewsAsync(previous.Id, cancellationToken);
        var movements = new Dictionary<int, DivisionMovement>();

        foreach (var view in views.Where(view => view.Tier is not null))
        {
            var collection = await _standings.CollectAsync(view.Id, view, cancellationToken);
            if (collection.Seeds.Count == 0)
            {
                continue;
            }

            var table = StandingTable.Build(collection.Seeds, collection.Finished);

            movements[view.Tier!.Value] = DivisionMovement.From(view.Tier.Value, table);
        }

        return movements;
    }

    /// <summary>
    /// The clubs that start the new season in a tier: the ones that stay in it, the ones
    /// coming up from below and the ones going down from above.
    ///
    /// <para>
    /// It is <see cref="Pyramid.ClubsOf"/> because where every club ends up is a rule of the
    /// pyramid rather than of the close, and a rule that lives in a private method of a
    /// service is a rule nothing can be asked about. It was also wrong here for a long time:
    /// a tier's own list was read and then what arrived was added on top, so the clubs
    /// leaving stayed as well as going, and each season's pyramid grew by every club that
    /// moved — sixteen became twenty-four, and the country became eighty-eight clubs.
    /// </para>
    /// </summary>
    private static IReadOnlyList<Guid> ClubsOf(
        int tier,
        IReadOnlyDictionary<int, DivisionMovement> movements,
        IReadOnlyList<Division> divisions) =>
        Pyramid.ClubsOf(tier, movements);

    private static string PositionName(int position) => position switch
    {
        1 => "Campeão",
        2 => "Vice",
        3 => "Terceiro",
        _ => $"{position}º lugar"
    };
}
