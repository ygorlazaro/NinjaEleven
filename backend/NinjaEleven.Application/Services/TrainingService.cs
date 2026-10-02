using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Players;
using NinjaEleven.Application.Models;
using System.Diagnostics;

namespace NinjaEleven.Application.Services;

/// <summary>What one training session did, so the screen that asked can be told rather than
/// having to work it out again from the squad it will refetch.</summary>
/// <param name="PlayerId">Who was trained.</param>
/// <param name="SeasonId">The season it was spent in.</param>
/// <param name="Attribute">What was worked on.</param>
/// <param name="EnergySpent">What the session cost.</param>
/// <param name="EnergyLeft">What he has afterwards.</param>
/// <param name="AttributeBefore">The attribute as it was.</param>
/// <param name="AttributeAfter">The attribute as it is.</param>
/// <param name="Fee">What the session cost the club.</param>
public record TrainingResult(
    Guid PlayerId,
    Guid SeasonId,
    PlayerAttribute Attribute,
    int EnergySpent,
    int EnergyLeft,
    int AttributeBefore,
    int AttributeAfter,
    decimal Fee);

/// <summary>One man on a manager's sheet: this one, this attribute, today.</summary>
/// <param name="PlayerId">Who to work.</param>
/// <param name="Attribute">What to work on.</param>
public record TrainingRequest(Guid PlayerId, PlayerAttribute Attribute);

/// <summary>
/// What happened to one man in a selection, which is worked or refused and never nothing.
/// </summary>
/// <param name="PlayerId">Who he is.</param>
/// <param name="Attribute">What was asked of him.</param>
/// <param name="Worked">Whether the session was run.</param>
/// <param name="Result">What it did, when it ran.</param>
/// <param name="RefusalCode">The rule that refused him, when it did.</param>
/// <param name="Refusal">The sentence telling the manager why, when it did.</param>
/// <remarks>
/// The refusal travels as the code and the sentence the domain wrote, and not as a client's
/// guess at the reason: a screen that invented "não pôde treinar" for a man who had no energy
/// and for a man who had already worked today would be telling the manager the same thing
/// about two problems he fixes in opposite ways.
/// </remarks>
public record TrainingOutcome(
    Guid PlayerId,
    PlayerAttribute Attribute,
    bool Worked,
    TrainingResult? Result,
    string? RefusalCode,
    string? Refusal)
{
    public static TrainingOutcome Trained(TrainingResult result) =>
        new(result.PlayerId, result.Attribute, true, result, null, null);

    public static TrainingOutcome Refused(
        Guid playerId,
        PlayerAttribute attribute,
        string code,
        string refusal) =>
        new(playerId, attribute, false, null, code, refusal);
}

/// <summary>
/// A manager's one decision about one player: work on something, and pay for it in the
/// energy that would otherwise have been spent on Saturday.
///
/// <para>
/// The whole design is that energy is the price, and that is a decision about what the
/// manager is choosing between. Training a man costs him the same energy a match takes out
/// of him, so the twenty-eight sessions that will turn a twenty-year-old into a first-choice
/// player are twenty-eight matchdays' worth of football he has decided not to play him in —
/// and the manager can see that on the same number he already reads, rather than being told
/// about it in a currency invented for the feature.
/// </para>
///
/// <para>
/// Nothing is invented here. The price comes from <see cref="TrainingRules"/>, the ceiling
/// comes from the player's own potential, the energy comes from his season state, and a
/// refusal is one of the codes a client can read rather than a sentence. A service that
/// worked out its own idea of what a session costs would be a second opinion about the same
/// rule, and the squad screen would then be quoting a different number from the one it
/// charges.
/// </para>
///
/// <para>
/// Both writes are in one commit. A session that took the energy and did not raise the
/// attribute would be a man who trained all week and came out of it no better, and a
/// session that raised the attribute and kept the energy would be free improvement — which is
/// why there is no order in which one of the two can be written without the other.
/// </para>
///
/// <para>
/// A session now costs the club as well as the man, and the energy cost scales with how
/// close the attribute is to its potential: pushing a youngster is cheap, and the last few
/// points of a veteran are the most expensive. The fee is a share of the man's season wage.
/// All four writes — the session, the energy, the point and the fee — go in together or none
/// of them do, because a club charged for a session it never had is the one failure in this
/// feature that nobody could undo.
/// </para>
/// </summary>
public class TrainingService
{
    private readonly IPlayerRepository _players;
    private readonly ISeasonRepository _seasons;
    private readonly ITeamRepository _teams;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ITrainingSessionRepository _sessions;
    private readonly IMatchDayRepository _matchDays;
    private readonly IFixtureRepository _fixtures;
    private readonly FinanceService _financeService;

    public TrainingService(
        IPlayerRepository players,
        ISeasonRepository seasons,
        ITeamRepository teams,
        IUnitOfWork unitOfWork,
        IClock clock,
        ITrainingSessionRepository sessions,
        IMatchDayRepository matchDays,
        IFixtureRepository fixtures,
        FinanceService financeService)
    {
        _players = players;
        _seasons = seasons;
        _teams = teams;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _sessions = sessions;
        _matchDays = matchDays;
        _fixtures = fixtures;
        _financeService = financeService;
    }

    /// <summary>
    /// The club's whole training sheet in one read: every man, every attribute, and what a
    /// session on each would cost him.
    ///
    /// <para>
    /// The prices are computed here rather than sent and recomputed by the client, which is
    /// the same rule every other number in the game follows and the one this feature is most
    /// tempted to break: the cost is a product of the attribute, the potential, the stamina
    /// and a curve, and a client working it out would be a second implementation of
    /// <see cref="TrainingRules"/> that could disagree with it after any tuning. It would also
    /// be a second source of truth about what a manager's next click will cost him, which is
    /// the one number on the screen that has to be right before he presses it.
    /// </para>
    /// </summary>
    public async Task<SquadTrainingQuotes> QuoteAsync(
        Guid teamId,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        var season = seasonId is { } wanted
            ? await _seasons.GetAsync(wanted, cancellationToken)
            : await _seasons.GetCurrentAsync(cancellationToken);

        if (season is null)
        {
            throw new DomainValidationException(
                "SeasonRequired", "Não há temporada em andamento para treinar.");
        }

        var states = await _players.ListSeasonStatesAsync(season.Id, teamId, cancellationToken);
        var stateByPlayer = states.ToDictionary(state => state.PlayerId);

        // One read of the men rather than one per man: a squad of twenty-three asked a man at
        // a time is twenty-three queries to answer a question about the whole set.
        var squad = (await _players.ListAsync(cancellationToken))
            .Where(player => stateByPlayer.ContainsKey(player.Id))
            .ToList();

        // The wage is on the contract, so the price of a session is read from the same place
        // the wage on the squad screen is read from. Reading it from the player's attributes
        // here would price a session off a number the club is not paying this season. Academy
        // players have no contract, so the fee is worked from the value of the player he is
        // now — the same wage he would sign for on promotion.
        var contracts = await _teams.GetLiveContractsAsync(teamId, cancellationToken);
        var contractWages = contracts.ToDictionary(m => m.PlayerId, m => m.Wage);

        var wageByPlayer = new Dictionary<Guid, decimal>();
        foreach (var player in squad)
        {
            wageByPlayer[player.Id] = contractWages.GetValueOrDefault(player.Id);
            if (wageByPlayer[player.Id] == 0m && stateByPlayer[player.Id].IsAcademyPlayer)
            {
                wageByPlayer[player.Id] = PlayerValuation.SeasonWage(player, stateByPlayer[player.Id]);
            }
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        // The day's value is read once for the whole squad: it names the calendar matchday
        // a fee is written against, and whether the club is playing today.
        var day = await TheDayForAsync(teamId, season.Id, today, cancellationToken);
        var spentByPlayer = await _sessions.ListByTeamAndDayAsync(teamId, today, cancellationToken);

        var quotes = new SquadTrainingQuotes
        {
            TeamId = teamId,
            SeasonId = season.Id,
            SquadEnergy = states.Sum(state => state.Energy),
            Day = today,
            PlaysToday = day.Plays,
            Players = squad
                .OrderBy(player => player.Position)
                .ThenByDescending(player => player.Potential)
                .Select(player => QuoteFor(
                    player,
                    stateByPlayer[player.Id],
                    wageByPlayer.GetValueOrDefault(player.Id)))
                .ToList()
        };

        return quotes;
    }

    private static TrainingQuote QuoteFor(
        Player player,
        PlayerSeasonState state,
        decimal seasonWage)
    {
        var quote = new TrainingQuote
        {
            PlayerId = player.Id,
            Name = player.Name,
            Position = player.Position.ToString(),
            Age = player.Age,
            Potential = player.Potential,
            Stamina = player.Stamina,
            Energy = state.Energy,
            IsAvailable = state.IsAvailable,
            Injury = state.Injury.ToString(),
            IsAcademyPlayer = state.IsAcademyPlayer,
            SessionFee = TrainingRules.SessionFee(seasonWage)
        };

        foreach (var attribute in DevelopmentRules.All)
        {
            var trainable = TrainingRules.BelongsToThisMan(player, attribute)
                && !TrainingRules.IsAtCeiling(player, attribute);

            quote.Attributes.Add(new TrainingAttributeQuote
            {
                Attribute = attribute.ToString(),
                Value = player.Get(attribute),
                Cost = trainable ? TrainingRules.Cost(player, attribute) : null
            });
        }

        return quote;
    }

    /// <summary>
    /// Whether the club plays on a given day, and the calendar matchday it names if there is one.
    /// Training has no daily session cap now — a player may train as many times as his energy
    /// allows — so the only question the day answers is whether a fee should be written against
    /// a calendar day.
    /// </summary>
    private sealed record TrainingDay(bool Plays, MatchDay? MatchDay);

    private async Task<TrainingDay> TheDayForAsync(
        Guid teamId,
        Guid seasonId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var matchDay = (await _matchDays.ListBySeasonAsync(seasonId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Date == day);

        var fixtures = await _fixtures.ListByTeamAndDateAsync(teamId, day, cancellationToken);

        return new TrainingDay(fixtures.Count > 0, matchDay);
    }

    /// <summary>
    /// The next ordinal for a training session on a given day: the count of sessions already
    /// recorded for the club that day, which is the ordinal the new one takes.
    /// </summary>
    private async Task<int> NextOrdinalForAsync(
        Guid teamId,
        Guid seasonId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var sessions = await _sessions.ListByTeamAndDayAsync(teamId, day, cancellationToken);
        return sessions.Count;
    }

    /// <summary>
    /// Runs one session on one attribute for one player, in the current season unless the
    /// caller names another.
    /// </summary>
    public async Task<TrainingResult> TrainAsync(
        Guid playerId,
        PlayerAttribute attribute,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        var player = await _players.GetAsync(playerId, cancellationToken)
            ?? throw new EntityNotFoundException("Player", playerId);

        var season = seasonId is { } wanted
            ? await _seasons.GetAsync(wanted, cancellationToken)
            : await _seasons.GetCurrentAsync(cancellationToken);

        if (season is null)
        {
            throw new DomainValidationException(
                "SeasonRequired", "Não há temporada em andamento para treinar.");
        }

        if (!TrainingRules.BelongsToThisMan(player, attribute))
        {
            throw new DomainValidationException(
                "AttributeNotTrainable",
                $"{Describe(attribute)} não é um atributo de {player.Name}.");
        }

        if (TrainingRules.IsAtCeiling(player, attribute))
        {
            throw new DomainValidationException(
                "PotentialReached",
                $"{player.Name} já atingiu o potencial máximo em {Describe(attribute)}.");
        }

        // Read tracked: this is a command and it is going to write both the man and his
        // season, so the detached read the profile screen uses would hand back copies.
        var state = await _players.GetSeasonStateForUpdateAsync(playerId, season.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PlayerSeasonState", playerId);

        // An injured man training risks aggravating the injury, but the manager may choose to
        // train through it anyway — the feature no longer refuses on the basis of an existing
        // knock, it refuses only on the basis of a suspension and then applies the injury risk.
        if (state.Injury != Injury.None && state.InjuryMatchesRemaining > 0)
        {
            throw new DomainValidationException(
                "PlayerInjured",
                $"{player.Name} está machucado e não pode treinar.");
        }

        if (state.SuspensionMatches > 0)
        {
            throw new DomainValidationException(
                "PlayerNotAvailable",
                $"{player.Name} está suspenso e não pode treinar.");
        }

        // The allowance is the club's, and a man without a club in this season has no club to
        // be spending one. A free agent who has not signed cannot be put through a session
        // that somebody is going to be charged for.
        if (state.TeamId is not { } teamId)
        {
            throw new DomainValidationException(
                "PlayerNotContracted",
                $"{player.Name} não tem clube nesta temporada para treinar.");
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var matchDay = await TheDayForAsync(teamId, season.Id, today, cancellationToken);

        var cost = TrainingRules.Cost(player, attribute);

        if (state.Energy < cost)
        {
            throw new DomainValidationException(
                "NotEnoughEnergy",
                $"{player.Name} precisa de {cost} de energia para treinar {Describe(attribute)} " +
                $"e tem {state.Energy}.");
        }

        // The fee is a share of the wage the club has agreed, not of a wage worked out from the
        // man as he is right now: a striker who scored twice this week costs the same to train
        // as he did on Monday, and a club that has signed a man to a number is not re-opening
        // that number every time he has a good afternoon. Academy players have no contract, so
        // the fee is worked from the value of the player he is right now — the same wage he would
        // sign for on promotion.
        var contract = (await _teams.GetLiveContractsAsync(teamId, cancellationToken))
            .FirstOrDefault(membership => membership.PlayerId == player.Id);

        decimal wage = contract?.Wage ?? 0m;
        if (wage == 0m && state.IsAcademyPlayer)
        {
            wage = PlayerValuation.SeasonWage(player, state);
        }

        var fee = TrainingRules.SessionFee(wage);
        var before = player.Get(attribute);
        var now = _clock.UtcNow;

        player.Set(attribute, before + 1);
        state.DrainEnergy(cost);

        // Every session carries an injury risk. A young player with good stamina is more
        // resilient than an older one with less in the tank, which is what the ratio says.
        var injury = TrainingRules.RollInjury(player, state);
        if (injury is not null)
        {
            var matches = TrainingRules.InjuryDuration(player.Age, injury.Value);
            state.AddInjury(injury.Value, matches);
        }

        var ordinal = await NextOrdinalForAsync(teamId, season.Id, today, cancellationToken);

        var session = TrainingSession.Create(
            player.Id,
            teamId,
            season.Id,
            today,
            now,
             matchDay.MatchDay?.Id,
            attribute,
            cost,
            fee,
            ordinal);

        await _sessions.AddAsync(session, cancellationToken);

        // Staged rather than appended: the fee, the session, the energy and the point are one
        // event, and a line that reached the book on its own would be a club charged for a
        // session it never had. The reference is the session's own id, so a retried command
        // pays once rather than twice for the same morning.
        var line = await _financeService.StageMovementAsync(
            teamId,
            season.Id,
            matchDay.MatchDay?.Number,
            FinanceMovementKind.Training,
            $"Treino de {player.Name} — {Describe(attribute)} {before} → {before + 1}",
            -fee,
            matchId: null,
            cancellationToken,
            reference: TrainingReference(session.Id));

        _players.Update(player);
        _players.UpdateSeasonState(state);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Nothing is reported to the box. A training fee is one of the club's weekly outgoings
        // and it is in the statement with the wages and the gate beside it; a message per
        // session would be four a week about a number the manager chose on purpose and can
        // see the cost of on the button he pressed. The session is spent, the point is gained
        // and the money is gone by this point, and none of that is news.
        Debug.Assert(line.Amount < 0m, "A training fee is a payment, so it leaves the club.");

        return new TrainingResult(
            player.Id,
            season.Id,
            attribute,
            cost,
            state.Energy,
            before,
            player.Get(attribute),
            fee);
    }

    /// <summary>
    /// Works a whole selection of men in one press of the button.
    ///
    /// <para>
    /// Each man is his own session and his own commit, and one man's refusal never takes
    /// another man's morning with it. That is the whole reason this is a loop over
    /// <see cref="TrainAsync"/> rather than a set operation: a session is four writes that go
    /// together, and a batch that rolled the eleven back because the twelfth was injured would
    /// be a manager who cannot work his squad because one reserve has a twisted ankle.
    /// </para>
    ///
    /// <para>
    /// Every man is answered for, in the order he was picked, whether he was worked or refused —
    /// a selection that silently lost somebody would be a screen that cannot say why the eleven
    /// it was given is not the eleven on the pitch.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<TrainingOutcome>> TrainManyAsync(
        IReadOnlyList<TrainingRequest> selection,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var outcomes = new List<TrainingOutcome>(selection.Count);

        foreach (var request in selection)
        {
            try
            {
                outcomes.Add(TrainingOutcome.Trained(
                    await TrainAsync(request.PlayerId, request.Attribute, seasonId, cancellationToken)));
            }
            catch (DomainValidationException refusal)
            {
                outcomes.Add(TrainingOutcome.Refused(request.PlayerId, request.Attribute, refusal.Code, refusal.Message));
            }
        }

        return outcomes;
    }

    /// <summary>
    /// The key that ties a fee in the club's book to the session that caused it, keyed by the
    /// session and not by the day.
    ///
    /// <para>
    /// Keying by the day would be wrong rather than merely coarse: a day is not the unit of
    /// payment, the session is, and a reference of the day would make the second session of a
    /// rest day look like the first one coming round again. A reference of the session is also
    /// what makes the pair checkable — a line in the book naming a session that is in the
    /// training history is a payment with a reason, and one naming nothing is not.
    /// </para>
    /// </summary>
    public static string TrainingReference(Guid sessionId) => $"training:{sessionId}";

    private static string Describe(PlayerAttribute attribute) => attribute switch
    {
        PlayerAttribute.Speed => "velocidade",
        PlayerAttribute.Accuracy => "finalização",
        PlayerAttribute.Dribbling => "drible",
        PlayerAttribute.Heading => "cabeceio",
        PlayerAttribute.Strength => "força",
        PlayerAttribute.GoalkeeperPower => "agilidade de goleiro",
        PlayerAttribute.Reflexes => "reflexos",
        _ => "estamina"
    };
}
