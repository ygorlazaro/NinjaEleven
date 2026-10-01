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
/// <param name="SessionsLeft">How many sessions this man has left on the day.</param>
public record TrainingResult(
    Guid PlayerId,
    Guid SeasonId,
    PlayerAttribute Attribute,
    int EnergySpent,
    int EnergyLeft,
    int AttributeBefore,
    int AttributeAfter,
    decimal Fee,
    int SessionsLeft);

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
/// A session now costs the club as well as the man, and it is limited: an allowance of one
/// session on a matchday and two on a rest day, counted from the sessions themselves, and a
/// fee of a share of the man's wage. All four writes — the session, the energy, the point and
/// the fee — go in together or none of them do, because a club charged for a session it never
/// had is the one failure in this feature that nobody could undo.
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
        // here would price a session off a number the club is not paying this season.
        var contracts = await _teams.GetLiveContractsAsync(teamId, cancellationToken);
        var wageByPlayer = contracts.ToDictionary(membership => membership.PlayerId, membership => membership.Wage);

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        // The day's value is the same for every man, so it is worked out once and the spent
        // count is taken per man: the day is what a body can take, not what a club can spend.
        var day = await TheAllowanceForAsync(teamId, Guid.Empty, season.Id, today, cancellationToken);
        var spentByPlayer = await _sessions.ListByTeamAndDayAsync(teamId, today, cancellationToken);

        var spent = spentByPlayer
            .GroupBy(session => session.PlayerId)
            .ToDictionary(group => group.Key, group => group.Count());

        var quotes = new SquadTrainingQuotes
        {
            TeamId = teamId,
            SeasonId = season.Id,
            SquadEnergy = states.Sum(state => state.Energy),
            Day = today,
            PlaysToday = day.Plays,
            SessionsAllowed = day.Allowed,
            SessionsSpent = spent.Count,
            Players = squad
                .OrderBy(player => player.Position)
                .ThenByDescending(player => player.Potential)
                .Select(player => QuoteFor(
                    player,
                    stateByPlayer[player.Id],
                    wageByPlayer.GetValueOrDefault(player.Id),
                    day.Allowed - spent.GetValueOrDefault(player.Id)))
                .ToList()
        };

        return quotes;
    }

    private static TrainingQuote QuoteFor(
        Player player,
        PlayerSeasonState state,
        decimal seasonWage,
        int sessionsLeft)
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
            SessionFee = TrainingRules.SessionFee(seasonWage),
            SessionsLeft = Math.Max(0, sessionsLeft)
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
    /// One man's training allowance for one day: whether his club is playing, how many
    /// sessions he has, how many are gone, and the matchday the day is, if the calendar has one.
    /// </summary>
    /// <param name="Plays">Whether the club has a fixture on the day.</param>
    /// <param name="Allowed">The sessions the day is worth.</param>
    /// <param name="Spent">The sessions he has already run.</param>
    /// <param name="MatchDay">The calendar's day, when there is one.</param>
    private sealed record Allowance(bool Plays, int Allowed, int Spent, MatchDay? MatchDay)
    {
        /// <summary>What is left to spend, never below zero so a screen can print it as it is.</summary>
        public int Left => Math.Max(0, Allowed - Spent);
    }

    /// <summary>
    /// One man's day, counted from his own sessions rather than from a tally.
    ///
    /// <para>
    /// Three set reads and a count between them: the club's fixtures on the day, the man's
    /// sessions on the day, and the calendar's own day — which is only opened to name the day a
    /// fee is written against, and is null on a day the calendar has no day for rather than
    /// invented so that a statement always has a day in it.
    /// </para>
    ///
    /// <para>
    /// The count is over one man and not over the club. It was over the club, which made a day
    /// worth one session to twenty-three men, and a manager who wanted his squad worked had to
    /// choose which nineteen to leave alone. A day is what a body can take, so the day is
    /// counted per body.
    /// </para>
    /// </summary>
    private async Task<Allowance> TheAllowanceForAsync(
        Guid teamId,
        Guid playerId,
        Guid seasonId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var matchDay = (await _matchDays.ListBySeasonAsync(seasonId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Date == day);

        var fixtures = await _fixtures.ListByTeamAndDateAsync(teamId, day, cancellationToken);
        var plays = fixtures.Count > 0;

        var spent = (await _sessions.ListByPlayerAndDayAsync(playerId, day, cancellationToken)).Count;

        return new Allowance(plays, TrainingRules.DailyBudget(plays), spent, matchDay);
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

        // An injured man cannot be put through a session, and neither can one suspended — the
        // same availability the lineup screen refuses to select him on. Training around an
        // injury would be the feature quietly deciding that a broken leg is a scheduling
        // inconvenience.
        if (!state.IsAvailable)
        {
            throw new DomainValidationException(
                "PlayerNotAvailable",
                $"{player.Name} não está disponível para treinar.");
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
        var allowance = await TheAllowanceForAsync(teamId, player.Id, season.Id, today, cancellationToken);

        // The refusal comes before the price is quoted, because a man who has spent the day's
        // allowance is not being offered anything and should not be shown what it would have
        // cost first. The count is the sessions themselves, so this cannot disagree with the
        // history it is counting.
        if (allowance.Left <= 0)
        {
            throw new DomainValidationException(
                "TrainingAllowanceSpent",
                allowance.Plays
                    ? $"{player.Name} já usou a {TrainingRules.SessionsOnAMatchDay} sessão de hoje e joga hoje."
                    : $"{player.Name} já usou as {TrainingRules.SessionsOnARestDay} sessões de hoje.");
        }

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
        // that number every time he has a good afternoon.
        var contract = (await _teams.GetLiveContractsAsync(teamId, cancellationToken))
            .FirstOrDefault(membership => membership.PlayerId == player.Id);

        var fee = TrainingRules.SessionFee(contract?.Wage ?? 0m);
        var before = player.Get(attribute);
        var now = _clock.UtcNow;

        player.Set(attribute, before + 1);
        state.DrainEnergy(cost);

        var session = TrainingSession.Create(
            player.Id,
            teamId,
            season.Id,
            today,
            now,
            allowance.MatchDay?.Id,
            attribute,
            cost,
            fee,
            allowance.Spent);

        await _sessions.AddAsync(session, cancellationToken);

        // Staged rather than appended: the fee, the session, the energy and the point are one
        // event, and a line that reached the book on its own would be a club charged for a
        // session it never had. The reference is the session's own id, so a retried command
        // pays once rather than twice for the same morning.
        var line = await _financeService.StageMovementAsync(
            teamId,
            season.Id,
            allowance.MatchDay?.Number,
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
            fee,
            allowance.Left - 1);
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
