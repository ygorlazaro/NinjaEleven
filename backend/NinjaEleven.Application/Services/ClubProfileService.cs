using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

/// <summary>
/// A club's own page, assembled whole: who it is, who runs it, what it costs to keep, what it
/// has won, where it has been and what has happened to it.
/// </summary>
/// <remarks>
/// <para>
/// The screen this serves used to read a stand-in for most of itself, and the numbers in that
/// stand-in were drawn from the club's own id so that two clubs did not look alike. That is
/// exactly the property that made it dangerous: a page whose invented history is stable per
/// club is a page that looks correct forever and is wrong from the first season, and a manager
/// reading it cannot tell it from a page that worked.
/// </para>
///
/// <para>
/// So every number here is read out of the world rather than drawn from it, and the two
/// families of fact are treated differently on purpose. <b>Results</b> — a founding, a
/// promotion, a relegation, a title, a season's top scorer — are derived when they are asked
/// for, out of the tables that already hold them, because a second copy of a result is a second
/// opinion about it and the two drift apart. <b>Decisions</b> — a rename, a new crest, new
/// colours — are the ones nothing in the world keeps, and those are rows in
/// <c>club_events</c>, written by the same call that performed the change.
/// </para>
///
/// <para>
/// The whole page is a fixed number of set-reads and no read inside a loop. A club's career is
/// twenty-odd editions and a hundred match lines, and asking for them one at a time is a page
/// that takes half a second and makes a hundred round trips to say four sentences.
/// </para>
/// </remarks>
public class ClubProfileService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly ITrophyRepository _trophyRepository;
    private readonly IClubEventRepository _clubEventRepository;
    private readonly IFinanceRepository _financeRepository;

    public ClubProfileService(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository,
        ICompetitionRepository competitionRepository,
        ITrophyRepository trophyRepository,
        IClubEventRepository clubEventRepository,
        IFinanceRepository financeRepository)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
        _competitionRepository = competitionRepository;
        _trophyRepository = trophyRepository;
        _clubEventRepository = clubEventRepository;
        _financeRepository = financeRepository;
    }

    /// <summary>
    /// A club, whole.
    /// </summary>
    /// <param name="teamId">The club: the manager's own, or anybody else's.</param>
    /// <param name="seasonId">
    /// The season the roster is counted in, when the caller has one in mind.
    ///
    /// <para>
    /// It moves exactly one number — how many men are on the books — and nothing else. The
    /// balance is deliberately not narrowed to a season, the shelf is the club's whole career,
    /// and the history is read from the beginning of time. A page that changed its whole face
    /// with the season would answer "what is this club like" with "what did it look like in the
    /// one season you happened to ask about".
    /// </para>
    /// </param>
    public async Task<ClubProfile> GetProfileAsync(
        Guid teamId,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        // The roster's season is the one number the caller's season decides. When the caller has
        // none — a page reached from a crest, before anybody asked which season it is — the
        // season the world is playing is the season a club's size is a fact about.
        var squadSeasonId = seasonId
            ?? (await _seasonRepository.GetCurrentAsync(cancellationToken))?.Id;

        var squad = squadSeasonId is { } season
            ? await _teamRepository.GetSquadAsync(teamId, season, cancellationToken)
            : Array.Empty<TeamMembership>();

        var lastMovement = await _financeRepository.GetLastAsync(teamId, cancellationToken);

        // One read of every season the world has rather than one per season the page touches.
        // The set is small and belongs to nobody, and the alternative is a page that asks the
        // database the same two facts about a season once for the shelf, once for the founding
        // and once for each movement.
        var seasons = await _seasonRepository.ListAsync(cancellationToken);
        var seasonById = seasons.ToDictionary(item => item.Id);

        var trophies = await _trophyRepository.ListByTeamAsync(teamId, cancellationToken);
        var recorded = await _clubEventRepository.ListByTeamAsync(teamId, cancellationToken);
        var divisions = await _teamRepository.ListDivisionSeasonsAsync(teamId, cancellationToken);

        // Every edition the club's shelf and its artilharia need named, read once. The names a
        // medal and a title are printed with are properties of the edition they were won in,
        // and there is no column on either trophy or participation carrying them.
        var editionSeasons = divisions
            .Select(entry => entry.SeasonId)
            .Concat(trophies.Select(trophy => trophy.SeasonId))
            .Distinct()
            .ToList();

        var editions = editionSeasons.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<CompetitionSeasonView>>()
            : await _competitionRepository.ListSeasonViewsAsync(editionSeasons, cancellationToken);

        var views = editions.Values
            .SelectMany(seasonViews => seasonViews)
            .ToDictionary(view => view.Id);

        var history = new List<ClubHistoryEvent>();

        history.AddRange(ReadTheFounding(divisions, seasonById));
        history.AddRange(ReadTheMovements(divisions, seasonById));
        history.AddRange(ReadTheTitles(trophies, views, seasonById));
        history.AddRange(await ReadTheTopScorers(teamId, divisions, views, seasonById, cancellationToken));
        history.AddRange(ReadTheRecorded(recorded, seasonById));

        // Newest first, because a career is read from now backwards: the moment a manager is
        // living through is the first line he sees and the season the club began is the last.
        // The season is the primary key and the id breaks a tie inside one, so a page read twice
        // does not reorder two moments of the same season underneath the manager reading them.
        return new ClubProfile
        {
            TeamId = team.Id,
            Name = team.Name,
            ShortName = team.ShortName,
            PrimaryColor = team.PrimaryColor,
            SecondaryColor = team.SecondaryColor,
            // The club's managers come back with the club, so the man running it is not a
            // fifth read of a row the first one already had. The latest is the one in charge: a
            // club that has been taken over still carries the manager it had before, and a page
            // naming the wrong one would name a man who does not run this club.
            CoachName = team.Managers
                .OrderByDescending(manager => manager.StartedAt)
                .Select(manager => manager.Name)
                .FirstOrDefault() ?? string.Empty,
            SquadSize = squad.Count,
            // The ledger's last line and not a sum of it, which is the same rule as the balance
            // endpoint and for the same reason: a running balance and the sum of a ledger are
            // two numbers the day one of them drifts, and this is what wages are paid against.
            Balance = lastMovement?.BalanceAfter ?? 0m,
            Promotions = history.Count(entry => entry.Kind == ClubHistoryKind.Promotion),
            Relegations = history.Count(entry => entry.Kind == ClubHistoryKind.Relegation),
            Trophies = ReadTheShelf(trophies, divisions, views, seasonById),
            History = history
                .OrderByDescending(entry => entry.SeasonNumber ?? 0)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .ToList()
        };
    }

    /// <summary>
    /// The shelf, with every medal carrying the season and the division it was won in.
    /// </summary>
    /// <remarks>
    /// The division is the division the club was <em>in</em> that season, taken from its own
    /// participation, and not from where it plays now. That is the whole reason a club can be
    /// relegated out of the division it won: copying today's tier onto last season's medal
    /// would move the club's own history down a division every time it dropped, and a shelf of
    /// titles that quietly changes its mind is worse than no shelf.
    ///
    /// <para>
    /// A cup medal is named by the competition and a division's by the division, because
    /// "Campeonato Brasileiro" on a gold disc says nothing a manager can act on while "1ª
    /// Divisão" says where the title was won and therefore where the club has been.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<ClubTrophy> ReadTheShelf(
        IReadOnlyList<TrophyAward> trophies,
        IReadOnlyList<ClubDivisionSeason> divisions,
        IReadOnlyDictionary<Guid, CompetitionSeasonView> views,
        IReadOnlyDictionary<Guid, Season> seasonById)
    {
        var tierOfSeason = divisions.ToDictionary(entry => entry.SeasonId, entry => entry.Tier);

        return trophies
            .Select(trophy =>
            {
                var season = seasonById.GetValueOrDefault(trophy.SeasonId);
                var view = views.GetValueOrDefault(trophy.CompetitionSeasonId);
                var tier = tierOfSeason.GetValueOrDefault(trophy.SeasonId);
                var divisionName = trophy.DivisionId is null
                    ? null
                    : CompetitionRules.DivisionName(tier);

                return new ClubTrophy
                {
                    Id = trophy.Id,
                    // A division's own name when there is one, and the competition's own name
                    // when there is not — which is the cup, the Supercup, and anything else the
                    // game may add that is not a table of sixteen clubs.
                    Competition = divisionName ?? view?.Name ?? string.Empty,
                    Kind = trophy.Kind,
                    SeasonId = trophy.SeasonId,
                    SeasonNumber = season?.Number ?? 0,
                    SeasonName = season?.Name ?? string.Empty,
                    DivisionName = divisionName,
                    DivisionTier = trophy.DivisionId is null ? null : tier
                };
            })
            .OrderByDescending(trophy => trophy.SeasonNumber)
            .ThenBy(trophy => trophy.Kind)
            .ToList();
    }

    /// <summary>
    /// The club's first season in the pyramid.
    /// </summary>
    /// <remarks>
    /// Derived and therefore the earliest row that exists, rather than a row somebody wrote
    /// down. A club in its first season has this and nothing else, which is the correct history
    /// of a club that has just been founded: one line, where an empty page would be a page
    /// saying nothing about the one thing a new club is.
    /// </remarks>
    private static IReadOnlyList<ClubHistoryEvent> ReadTheFounding(
        IReadOnlyList<ClubDivisionSeason> divisions,
        IReadOnlyDictionary<Guid, Season> seasonById)
    {
        var first = divisions
            .Select(entry => (Entry: entry, Season: seasonById.GetValueOrDefault(entry.SeasonId)))
            .Where(pair => pair.Season is not null)
            .OrderBy(pair => pair.Season!.Number)
            .FirstOrDefault();

        if (first.Season is null)
        {
            return Array.Empty<ClubHistoryEvent>();
        }

        return new[]
        {
            new ClubHistoryEvent
            {
                Id = $"{first.Season.Id}-first-season",
                Kind = ClubHistoryKind.FirstSeason,
                SeasonId = first.Season.Id,
                SeasonNumber = first.Season.Number,
                SeasonName = first.Season.Name,
                Description = $"Primeira temporada na pirâmide, na {CompetitionRules.DivisionName(first.Entry.Tier)}.",
                Value = first.Entry.Tier
            }
        };
    }

    /// <summary>
    /// Every time the club changed division, in either direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A promotion and a relegation are the same fact read twice — "this season's tier is not
    /// last season's" — and the direction comes from which way the tier moved. The rule is the
    /// domain's and is not written here again: <see cref="ClubMovement"/> is what decides that
    /// a lower tier is higher up the pyramid, and a second opinion about which way is up would
    /// eventually disagree with the pyramid's.
    /// </para>
    /// <para>
    /// Consecutive seasons only, because a gap is not a relegation. The world draws the
    /// pyramid one season at a time out of the season before it, so a club that skipped a
    /// season was not climbing past a division it never played in, and comparing across the
    /// gap would invent a move in each direction.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<ClubHistoryEvent> ReadTheMovements(
        IReadOnlyList<ClubDivisionSeason> divisions,
        IReadOnlyDictionary<Guid, Season> seasonById)
    {
        var events = new List<ClubHistoryEvent>();

        var ordered = divisions
            .Select(entry => (Entry: entry, Season: seasonById.GetValueOrDefault(entry.SeasonId)))
            .Where(pair => pair.Season is not null)
            .OrderBy(pair => pair.Season!.Number)
            .ToList();

        for (var index = 1; index < ordered.Count; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];

            if (current.Season!.Number != previous.Season!.Number + 1)
            {
                continue;
            }

            // The team id is a formality: the rule that decides the direction is the tiers, and
            // the one that reads them is the domain's. Handing it this club's id keeps the call
            // honest about being that club's movement.
            var direction = new ClubMovement(
                Guid.Empty,
                previous.Entry.Tier,
                current.Entry.Tier,
                0).Direction;

            if (direction == MovementDirection.None)
            {
                continue;
            }

            var promoted = direction == MovementDirection.Promoted;
            var to = CompetitionRules.DivisionName(current.Entry.Tier);

            events.Add(new ClubHistoryEvent
            {
                // Both seasons, because this event is two rows in the world and not one. An id
                // built from only one of them could not tell two promotions apart.
                Id = $"{previous.Season.Id}-{current.Season.Id}-division",
                Kind = promoted ? ClubHistoryKind.Promotion : ClubHistoryKind.Relegation,
                SeasonId = current.Season.Id,
                SeasonNumber = current.Season.Number,
                SeasonName = current.Season.Name,
                Description = promoted
                    ? $"Acesso à {to}."
                    : $"Rebaixamento para a {to}.",
                Value = current.Entry.Tier
            });
        }

        return events;
    }

    /// <summary>
    /// Every title, from the trophy awards.
    /// </summary>
    /// <remarks>
    /// The champion's rows only. The shelf shows second and third place as well, but a history
    /// is not a shelf: a runner-up is a place in a table and not a moment in a club's life, and
    /// a page that listed every podium the club ever reached would say the same kind of thing
    /// three times a season and bury the title under it.
    /// </remarks>
    private static IReadOnlyList<ClubHistoryEvent> ReadTheTitles(
        IReadOnlyList<TrophyAward> trophies,
        IReadOnlyDictionary<Guid, CompetitionSeasonView> views,
        IReadOnlyDictionary<Guid, Season> seasonById)
    {
        return trophies
            .Where(trophy => trophy.Kind == TrophyKind.Champion)
            .Select(trophy =>
            {
                var season = seasonById.GetValueOrDefault(trophy.SeasonId);
                var view = views.GetValueOrDefault(trophy.CompetitionSeasonId);
                var name = trophy.DivisionId is null
                    ? view?.Name
                    : view?.Name ?? string.Empty;

                return new ClubHistoryEvent
                {
                    Id = $"{trophy.Id}-title",
                    Kind = ClubHistoryKind.Title,
                    SeasonId = trophy.SeasonId,
                    SeasonNumber = season?.Number ?? 0,
                    SeasonName = season?.Name ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(name)
                        ? "Campeão da competição."
                        : $"Campeão: {name}.",
                    Value = trophy.Position
                };
            })
            .ToList();
    }

    /// <summary>
    /// Every season in which one of the club's men topped a competition's scoring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one part of the history that costs real football to answer, and it is
    /// answered as a set rather than one edition at a time. A club in the pyramid for five
    /// seasons is enrolled in five divisions' editions and five cups' — twenty editions, and the
    /// obvious way to ask is twenty queries of a walk through every match line of a season.
    /// </para>
    /// <para>
    /// Every club's lines in every one of those editions are read together, because an
    /// artilharia is ranked over everybody in the edition: a reader that asked for this club's
    /// men first would rank a division of sixteen down to the two players of one club and crown
    /// whichever of them scored most, which is not the artilharia of anything. The chart is then
    /// settled by <see cref="TopScorerTable.Rank"/> — the same chain, in the same order, that
    /// the prize list is settled by — so the man a club's page calls its top scorer and the man
    /// the game paid are the same man on the same grounds.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<ClubHistoryEvent>> ReadTheTopScorers(
        Guid teamId,
        IReadOnlyList<ClubDivisionSeason> divisions,
        IReadOnlyDictionary<Guid, CompetitionSeasonView> views,
        IReadOnlyDictionary<Guid, Season> seasonById,
        CancellationToken cancellationToken)
    {
        if (divisions.Count == 0)
        {
            return Array.Empty<ClubHistoryEvent>();
        }

        var editionIds = divisions
            .Select(entry => entry.CompetitionSeasonId)
            .Concat(views.Values
                .Where(view => view.IsDivision || view.Type == CompetitionType.Cup)
                .Select(view => view.Id))
            .Distinct()
            .ToList();

        var lines = await _playerRepository.ListEditionScorerLinesForEditionsAsync(
            editionIds,
            cancellationToken);

        if (lines.Count == 0)
        {
            return Array.Empty<ClubHistoryEvent>();
        }

        // A man who changed clubs inside a season has a line per club, and an artilharia is one
        // line per man. He is counted once, under the club he scored most for in that edition,
        // and his games and cards come off that same line — so the chain that decides who is
        // the top scorer is settled on one club's football rather than on a career's.
        var onePerPlayer = lines
            .GroupBy(line => new { line.CompetitionSeasonId, line.PlayerId })
            .Select(group => group
                .OrderByDescending(line => line.Goals)
                .ThenBy(line => line.Appearances)
                .First())
            .ToList();

        var players = await _playerRepository.ListByIdsAsync(
            onePerPlayer.Select(line => line.PlayerId).Distinct().ToList(),
            cancellationToken);

        var playerById = players.ToDictionary(player => player.Id);
        var events = new List<ClubHistoryEvent>();

        foreach (var edition in onePerPlayer.GroupBy(line => line.CompetitionSeasonId))
        {
            var view = views.GetValueOrDefault(edition.Key);
            if (view is null)
            {
                continue;
            }

            // Ordered by name before it is ranked, so a pair level on every football ground is
            // printed in the order of the two names rather than in the order the rows came out
            // of the database. The name is the last thing a reader has and is not a rule that
            // outranks a season's football.
            var standings = edition
                .Where(line => playerById.ContainsKey(line.PlayerId))
                .Select(line => ScorerStanding.From(
                    line.PlayerId,
                    line.Goals,
                    line.Appearances,
                    line.YellowCards,
                    line.RedCards,
                    playerById[line.PlayerId].Age))
                .OrderBy(line => playerById[line.PlayerId].Name, StringComparer.Ordinal)
                .ToList();

            var first = TopScorerTable.Rank(standings).FirstOrDefault();
            if (first is null)
            {
                continue;
            }

            // The chart of the whole edition, and then the club's own share of it. A striker who
            // topped his own division is the club's top scorer; a second-division forward who
            // outscored him across the country is the country's top scorer and not this club's.
            var source = edition.FirstOrDefault(line => line.PlayerId == first.PlayerId);
            if (source is null || source.TeamId != teamId)
            {
                continue;
            }

            events.Add(new ClubHistoryEvent
            {
                Id = $"{edition.Key}-top-scorer",
                Kind = ClubHistoryKind.TopScorer,
                SeasonId = view.SeasonId,
                SeasonNumber = seasonById.GetValueOrDefault(view.SeasonId)?.Number ?? 0,
                SeasonName = seasonById.GetValueOrDefault(view.SeasonId)?.Name ?? string.Empty,
                Description = $"{playerById[first.PlayerId].Name} foi o artilheiro de {view.Name} com {source.Goals} gols.",
                Value = source.Goals
            });
        }

        return events;
    }

    /// <summary>
    /// The moments that were recorded at the time, put into words.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A rename, a crest and a colour change leave nothing else in the world to derive them
    /// from, so they are rows written by the same call that performed the change. What the row
    /// carries is the fact — the two values, before and after — and the sentence is built here
    /// rather than stored, because a sentence is a reading of a fact: a rename from a name the
    /// table does not hold can only be worded from the new name, and a row that carried prose
    /// would either have said nothing or invented the half it was missing.
    /// </para>
    /// <para>
    /// A recorded moment with no season is dated from when it happened rather than dropped, and
    /// it sorts as the oldest thing on the page. A manager renames a club between seasons quite
    /// often, and a history that threw those moments away would be a history of the football
    /// only — which is the half a manager is already looking at.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<ClubHistoryEvent> ReadTheRecorded(
        IReadOnlyList<ClubEvent> recorded,
        IReadOnlyDictionary<Guid, Season> seasonById)
    {
        var events = new List<ClubHistoryEvent>();

        foreach (var clubEvent in recorded)
        {
            // A kind this service has never seen is a row written by a version of the game that
            // knew something this one does not. It is left off the page rather than printed as
            // a moment with no name: the sentence would be invented, and an invented moment on a
            // club's history is the exact thing this page stopped doing.
            if (!SayWhich(clubEvent.Kind, out var kind))
            {
                continue;
            }

            var season = clubEvent.SeasonId is { } seasonId
                ? seasonById.GetValueOrDefault(seasonId)
                : null;

            events.Add(new ClubHistoryEvent
            {
                Id = clubEvent.Id.ToString(),
                Kind = kind,
                SeasonId = clubEvent.SeasonId,
                SeasonNumber = season?.Number,
                SeasonName = season?.Name,
                Description = SayIt(clubEvent),
                Value = null
            });
        }

        return events;
    }

    /// <summary>
    /// The page's name for a recorded kind, and whether there is one at all.
    /// </summary>
    private static bool SayWhich(ClubEventKind kind, out ClubHistoryKind said)
    {
        said = kind switch
        {
            ClubEventKind.NameChange => ClubHistoryKind.NameChange,
            ClubEventKind.CrestChange => ClubHistoryKind.CrestChange,
            ClubEventKind.ColorsChange => ClubHistoryKind.ColorsChange,
            _ => default
        };

        return kind is ClubEventKind.NameChange
            or ClubEventKind.CrestChange
            or ClubEventKind.ColorsChange;
    }

    /// <summary>
    /// What a recorded moment is called, in this game's words.
    /// </summary>
    /// <remarks>
    /// A rename is the only one of the three with a name on either side of it, so it is the
    /// only one that can be told as a sentence: "Náutico de Guanabara passou a chamar-se
    /// Náutico" is the whole of what happened, and the page has the two names to say it with. A
    /// crest and a colour change have no before and after in words — one is a drawing and the
    /// other is a pair of hex codes — so those are said as what was done, which is all the fact
    /// they actually are.
    /// </remarks>
    private static string SayIt(ClubEvent clubEvent) => clubEvent.Kind switch
    {
        ClubEventKind.NameChange when clubEvent.PreviousValue is { } previous
            && clubEvent.NewValue is { } current =>
            $"{previous} passou a chamar-se {current}.",
        ClubEventKind.NameChange when clubEvent.NewValue is { } current =>
            $"O clube passou a chamar-se {current}.",
        ClubEventKind.CrestChange => clubEvent.NewValue is null
            ? "O escudo do clube foi retirado."
            : "Novo escudo, desenhado pelo técnico.",
        ClubEventKind.ColorsChange => clubEvent.NewValue is { } colors
            ? $"Novas cores: {colors}."
            : "As cores do clube foram alteradas.",
        _ => "O clube mudou."
    };
}