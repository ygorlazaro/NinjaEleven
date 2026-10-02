using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Application.Abstractions;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Use cases around clubs. It coordinates the repositories and decides what is valid;
/// it never writes SQL and it never returns entities straight to the transport layer.
/// </summary>
public class TeamService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IClubEventRepository _clubEventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TeamService(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        ISeasonRepository seasonRepository,
        IMatchRepository matchRepository,
        IClubEventRepository clubEventRepository,
        IUnitOfWork unitOfWork)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _seasonRepository = seasonRepository;
        _matchRepository = matchRepository;
        _clubEventRepository = clubEventRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Marks the club a manager is running, and unmarks whichever one was marked before.
    ///
    /// Almost the whole game treats every club the same, because the engine plays the club
    /// nobody is watching and a manager's club is not special to a fixture. It is special to
    /// exactly one thing — it answers for itself — and the market is the only reader of the
    /// mark. Without it the clubs nobody is watching would settle a man's transfer by a roll of
    /// the dice while the one club a person is playing was treated the same, which is the one
    /// decision in the game that has to belong to a person.
    /// </summary>
    public async Task<Team> TakeOverAsManagerClubAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var club = await _teamRepository.MarkAsManagerClubAsync(teamId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return club;
    }

    /// <summary>
    /// The last matches a club has finished, newest first, for the form guide on its card.
    /// A club that does not exist has no form, and saying so beats drawing an empty list
    /// for an id that was never one.
    /// </summary>
    public async Task<IReadOnlyList<TeamMatchRecord>> GetRecentMatchesAsync(
        Guid teamId,
        int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        return await _matchRepository.GetTeamHistoryAsync(teamId, TeamHistoryRules.Clamp(limit), cancellationToken);
    }

    /// <summary>
    /// Head-to-head matches between two clubs, newest first. A club's history against a
    /// specific rival is a different question from its general run, and the screen that asks
    /// for it has a different purpose: it is the story of this particular rivalry.
    /// </summary>
    public async Task<IReadOnlyList<TeamMatchRecord>> GetHeadToHeadAsync(
        Guid teamId,
        Guid opponentId,
        int limit = TeamHistoryRules.DefaultHistoryLength,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        if (await _teamRepository.GetAsync(opponentId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), opponentId);
        }

        return await _matchRepository.GetHeadToHeadAsync(teamId, opponentId, TeamHistoryRules.Clamp(limit), cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _teamRepository.ListAsync(cancellationToken);

    public async Task<Team> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _teamRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), id);

    /// <summary>
    /// Updates the name of a club that the manager has taken charge of.
    /// </summary>
    /// <remarks>
    /// The rename is recorded, and by this call rather than by anything that asks the club's
    /// page to remember it. <see cref="Team"/> keeps one name column, so without a row here a
    /// rename ends the story: the club's own history would show a founding and a title and no
    /// mention of the year the manager renamed it, and a page that could have said it and did
    /// not is indistinguishable from a game that never let him.
    /// </remarks>
    public async Task<Team> UpdateNameAsync(
        Guid teamId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var previousName = team.Name;

        team.SetName(name);
        _teamRepository.Update(team);

        await RecordAsync(
            team.Id,
            ClubEventKind.NameChange,
            previousName,
            team.Name,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return team;
    }

    /// <summary>
    /// Updates the kit colours of a club that the manager has taken charge of.
    /// </summary>
    public async Task<Team> UpdateColorsAsync(
        Guid teamId,
        string primaryColor,
        string secondaryColor,
        CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        var previousColors = $"{team.PrimaryColor} / {team.SecondaryColor}";

        team.SetPrimaryColor(primaryColor);
        team.SetSecondaryColor(secondaryColor);
        _teamRepository.Update(team);

        // Recorded for the same reason a rename is: two columns overwritten in place are the
        // whole of the world's memory of what this club used to look like.
        await RecordAsync(
            team.Id,
            ClubEventKind.ColorsChange,
            previousColors,
            $"{team.PrimaryColor} / {team.SecondaryColor}",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return team;
    }

    /// <summary>
    /// Stages one moment of the club's life against the change that just happened.
    /// </summary>
    /// <remarks>
    /// Staged and not saved: the moment and the change it describes are one fact, and a page
    /// that showed a rename without the new name — or a new name without the rename — would be
    /// a page that has half of an event. The unit of work that writes the club writes both.
    ///
    /// <para>
    /// The season is whatever is currently being played, and it may be none: a manager renames
    /// a club between seasons and the row is still worth having, so a null season is passed
    /// through rather than treated as a reason to skip.
    /// </para>
    /// </remarks>
    private async Task RecordAsync(
        Guid teamId,
        ClubEventKind kind,
        string? previousValue,
        string? newValue,
        CancellationToken cancellationToken)
    {
        var seasonId = (await _seasonRepository.GetCurrentAsync(cancellationToken))?.Id;

        await _clubEventRepository.AddAsync(
            ClubEvent.Create(teamId, kind, seasonId, previousValue, newValue),
            cancellationToken);
    }

    /// <summary>
    /// Draws the club's crest, or takes the one it has away.
    ///
    /// <para>
    /// Only the club a manager is running may be drawn. That check is here rather than in the
    /// controller because "the club a manager is running" is a fact about the world and not
    /// about the transport: the club screen, the club card and a background service all reach
    /// the same rule through the same door, and a rule that lived in the controller would be a
    /// rule the other two could walk past.
    /// </para>
    /// </summary>
    public async Task<Team> UpdateCrestAsync(
        Guid teamId,
        CrestDesign? crest,
        CancellationToken cancellationToken = default)
    {
        var team = await GetDrawableClubAsync(teamId, cancellationToken);

        var previousCrest = team.Crest;

        team.SetCrest(crest);
        _teamRepository.Update(team);

        // The crest is recorded whether it was drawn or taken away, and the difference is kept
        // in the row rather than decided here: whether the club now has a shield is the fact
        // itself, and a page that only knew "something was drawn" could not tell a new badge
        // from an empty one.
        await RecordAsync(
            team.Id,
            ClubEventKind.CrestChange,
            previousCrest is null ? null : "desenhado",
            crest is null ? null : "desenhado",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return team;
    }

    /// <summary>
    /// Draws the club's two shirts.
    ///
    /// <para>
    /// A second shirt that clashes with the first is still accepted. The club's manager decides
    /// what his club wears and the game decides when it is changed, and the screen is told why
    /// afterwards rather than being refused: a manager who is told his away shirt is too close
    /// to somebody else's home shirt learns something, and a manager whose drawing is refused
    /// learns that the editor is not his.
    /// </para>
    /// </summary>
    public async Task<Team> UpdateKitsAsync(
        Guid teamId,
        KitDesign homeKit,
        KitDesign? awayKit,
        CancellationToken cancellationToken = default)
    {
        var team = await GetDrawableClubAsync(teamId, cancellationToken);

        team.SetHomeKit(homeKit);
        team.SetAwayKit(awayKit);
        _teamRepository.Update(team);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return team;
    }

    private async Task<Team> GetDrawableClubAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetAsync(teamId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Team), teamId);

        if (!team.IsManagerClub)
        {
            throw new DomainValidationException(
                "NotTheManagerClub",
                "Só o clube que você comanda pode ter escudo e uniforme desenhados.");
        }

        return team;
    }

    /// <summary>
    /// Puts one of his own men in a shirt.
    ///
    /// <para>
    /// Three refusals, and each of them says which of the three things went wrong: a club that
    /// is not the manager's own, a player the club does not hold, and a number that is either
    /// outside one to ninety-nine or already on somebody else's back. The third is the one
    /// worth being precise about — "the number is taken" and "the number is not a number" are
    /// both refusals, and a manager who changed 9 to 100 and 9 to 10 needs to be told which
    /// of the two he did.
    /// </para>
    ///
    /// <para>
    /// The clash is asked of the club's whole set of live contracts rather than of a lookup by
    /// number, because the question is not "who has this number" but "is this number free",
    /// and a query that answered the first could not answer the second without also asking
    /// whether it found anybody. One read of the dressing room settles both.
    /// </para>
    /// </summary>
    public async Task<int> UpdateShirtNumberAsync(
        Guid teamId,
        Guid playerId,
        int shirtNumber,
        CancellationToken cancellationToken = default)
    {
        var team = await GetDrawableClubAsync(teamId, cancellationToken);

        if (!ShirtNumberRules.IsValid(shirtNumber))
        {
            throw new DomainValidationException(
                ShirtNumberRules.OutOfRangeCode,
                $"O número da camisa vai de {ShirtNumberRules.Lowest} a {ShirtNumberRules.Highest}.");
        }

        // The live contracts are the dressing room: an ended one keeps the number it was
        // given for the history, and does not stop anybody wearing it today.
        var contracts = await _teamRepository.GetLiveContractsAsync(teamId, cancellationToken);

        var contract = contracts.FirstOrDefault(membership => membership.PlayerId == playerId);
        if (contract is null)
        {
            throw new DomainValidationException(
                "PlayerNotContracted",
                "Esse jogador não está no elenco deste clube.");
        }

        var clash = contracts.FirstOrDefault(membership =>
            membership.Id != contract.Id && membership.ShirtNumber == shirtNumber);

        if (clash is not null)
        {
            throw new DomainValidationException(
                ShirtNumberRules.AlreadyTakenCode,
                $"A camisa {shirtNumber} já está em uso no elenco.");
        }

        contract.WearNumber(shirtNumber);
        _teamRepository.UpdateMembership(contract);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return shirtNumber;
    }

    /// <summary>
    /// Signs a man again, for as many seasons as the manager says and at whatever he is worth
    /// today.
    ///
    /// <para>
    /// The wage is worked out here rather than asked of the client, and that is the whole
    /// point of the rule: the salary on a contract is fixed for the run of that contract, so
    /// the only moment it moves is a renewal, and a renewal the client could price would be a
    /// renewal a client could make cheaper. The man is read as he is now — his age, his
    /// attributes, the cards and the injuries on his record — because that is what a club is
    /// buying when it signs him again.
    /// </para>
    ///
    /// <para>
    /// The seasons are counted from the season being played and not from what is left, so
    /// renewing a man with nothing left for three seasons gives him three and not none. A
    /// contract that has run out is ended by the season boundary rather than by this method,
    /// so a man here is always a man the club still holds.
    /// </para>
    /// </summary>
    public async Task<ContractRenewal> RenewContractAsync(
        Guid teamId,
        Guid playerId,
        int seasons,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        await GetDrawableClubAsync(teamId, cancellationToken);

        if (!ContractRules.IsARenewableLength(seasons))
        {
            throw new DomainValidationException(
                "InvalidContractLength",
                $"Uma renovação vai de {ContractRules.FewestRenewableSeasons} a " +
                $"{ContractRules.MostRenewableSeasons} temporadas; foram pedidas {seasons}.");
        }

        var season = seasonId is { } wanted
            ? await _seasonRepository.GetAsync(wanted, cancellationToken)
            : await _seasonRepository.GetCurrentAsync(cancellationToken);

        if (season is null)
        {
            throw new DomainValidationException(
                "SeasonRequired", "Não há temporada em andamento para renovar contrato.");
        }

        var player = await _playerRepository.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var state = await _playerRepository.GetSeasonStateForUpdateAsync(playerId, season.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        var contract = (await _teamRepository.GetLiveContractsAsync(teamId, cancellationToken))
            .FirstOrDefault(membership => membership.PlayerId == playerId);

        if (contract is null)
        {
            throw new DomainValidationException(
                "PlayerNotContracted",
                "Esse jogador não está no elenco deste clube.");
        }

        if (state.Retiring)
        {
            throw new DomainValidationException(
                "PlayerRetiring",
                "Esse jogador anunciou a aposentadoria ao final da temporada e não pode ter o contrato renovado.");
        }

        var wage = PlayerValuation.SeasonWage(player, state);
        contract.Renew(seasons, season.Number, wage, season.StartDate);

        _teamRepository.UpdateMembership(contract);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ContractRenewal(
            playerId,
            teamId,
            season.Id,
            contract.Id,
            contract.ContractSeasons,
            contract.SeasonsLeft(season.Number),
            wage);
    }

    public async Task<IReadOnlyList<SquadPlayer>> GetSquadAsync(        Guid teamId,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        // The season is not only checked for existence: a contract's clock is read against
        // the calendar, so how much of a player's contract is left is a question about the
        // season the squad is being asked for and not a number the table may work out.
        var season = await _seasonRepository.GetAsync(seasonId, cancellationToken)
            ?? throw new EntityNotFoundException("Season", seasonId);

        // One fact about the calendar for the whole squad: a release settlement counts the
        // rounds the club still has to pay for, and every man on the list is owed the same
        // number of them.
        var roundsLeft = await _seasonRepository.GetChampionshipRoundsLeftAsync(
            seasonId, cancellationToken);

        var memberships = await _teamRepository.GetSquadAsync(teamId, seasonId, cancellationToken);
        var squad = new List<SquadPlayer>(memberships.Count);

        if (memberships.Count == 0)
        {
            return squad;
        }

        // A squad is twenty-three men and a page of a club is read by everybody who looks at a
        // matchday, so the men and their season's state are read in two queries rather than two
        // per row. A player with a contract and no state in the season is skipped, exactly as
        // he was when he was read one at a time.
        var players = await _teamRepository.GetPlayersAsync(
            memberships.Select(membership => membership.PlayerId),
            cancellationToken);
        var states = (await _playerRepository.ListSeasonStatesByPlayerIdsAsync(
                seasonId,
                memberships.Select(membership => membership.PlayerId),
                cancellationToken))
            .ToDictionary(state => state.PlayerId);

        foreach (var membership in memberships)
        {
            if (!players.TryGetValue(membership.PlayerId, out var player))
            {
                continue;
            }

            if (!states.TryGetValue(player.Id, out var seasonState))
            {
                continue;
            }

            squad.Add(new SquadPlayer
            {
                Player = player,
                SeasonState = seasonState,
                Membership = membership,
                Season = season,
                RoundsLeftInSeason = roundsLeft
            });
        }

        return squad;
    }

    /// <summary>
    /// The club's scorers of a season, every man of the club who scored, and whether he is
    /// still there.
    ///
    /// Three things are decided here rather than left to the client, because all three are
    /// questions about the world and not about a table:
    ///
    /// - **What counts as a scorer.** Only the lines, and only goals: an own goal is a
    ///   defender's error and is carried apart. A table that added it would put a centre-back
    ///   on the list for the mistakes he made.
    /// - **Who is still at the club.** A membership with no end date. A scorer who has left is
    ///   kept in the answer with the flag against him, because a club's all-time scorers list
    ///   is the one page that must never lose a name — and a screen that only ever showed the
    ///   men under contract would quietly rewrite the club's history every time a window
    ///   opened.
    /// - **The order.** Goals first, then the fewest games for them, then the name. A striker
    ///   with eight goals in ten matches and one with eight in twenty are not the same
    ///   scorer, and the table that ranked them level would be hiding the whole difference.
    /// </summary>
    public async Task<IReadOnlyList<ClubScorerRow>> GetScorersAsync(
        Guid teamId,
        Guid seasonId,
        CompetitionType? competitionType = null,
        int topN = ScorerRules.DefaultScorers,
        CancellationToken cancellationToken = default)
    {
        if (await _teamRepository.GetAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Team), teamId);
        }

        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        var lines = await _playerRepository.ListClubScorerLinesAsync(
            teamId, seasonId, competitionType, cancellationToken);

        if (lines.Count == 0)
        {
            return Array.Empty<ClubScorerRow>();
        }

        var live = await _teamRepository.GetLiveContractsAsync(teamId, cancellationToken);
        var stillHere = live.Select(membership => membership.PlayerId).ToHashSet();
        var scorers = new List<ClubScorerRow>(lines.Count);
        var ageOf = new Dictionary<Guid, int>();

        // The men behind the lines are read in one go: a club's scorers list is a page about
        // its history, and a page of a hundred names asked for a hundred and one players.
        var players = await _teamRepository.GetPlayersAsync(
            lines.Select(line => line.PlayerId).Distinct(),
            cancellationToken);

        foreach (var line in lines)
        {
            if (!players.TryGetValue(line.PlayerId, out var player))
            {
                continue;
            }

            ageOf[player.Id] = player.Age;
            scorers.Add(new ClubScorerRow
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Age = player.Age,
                Goals = line.Goals,
                OwnGoals = line.OwnGoals,
                Started = line.Started,
                CameOn = line.CameOn,
                YellowCards = line.YellowCards,
                RedCards = line.RedCards,
                IsStillAtClub = stillHere.Contains(player.Id)
            });
        }

        // The chain is the domain's and the same one a division's or the cup's artilharia is
        // settled on: goals, then fewest games, then fewest cards by weight, then oldest. The
        // club's own list is not a different competition with a different order — a striker who
        // is level on goals with a team-mate is level with him for the same reasons on the club's
        // page as he is on the league's.
        //
        // The name is the print order of a pair the chain could not separate, and nothing more:
        // the positions come off the chain, so two men level on the whole of it are both first
        // rather than being told apart by the alphabet.
        var byId = scorers.ToDictionary(row => row.PlayerId);
        var standings = scorers
            .OrderBy(row => row.PlayerName, StringComparer.Ordinal)
            .Select(row => ScorerStanding.From(
                row.PlayerId,
                row.Goals,
                row.Started + row.CameOn,
                row.YellowCards,
                row.RedCards,
                ageOf.TryGetValue(row.PlayerId, out var age) ? age : null))
            .ToList();

        var ordered = TopScorerTable.Rank(standings)
            .Take(ScorerRules.Clamp(topN))
            .Select(line =>
            {
                var row = byId[line.PlayerId];
                row.Position = line.Position;
                row.TiedWith = line.TiedWith;
                row.GoalsPerAppearance = row.Started + row.CameOn > 0
                    ? Math.Round((double)row.Goals / (row.Started + row.CameOn), 2)
                    : null;

                return row;
            })
            .ToList();

        return ordered;
    }
}
