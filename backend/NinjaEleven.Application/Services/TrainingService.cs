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
/// <param name="SessionsLeft">How many sessions the club has left on the day.</param>
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ITrainingSessionRepository _sessions;
    private readonly IMatchDayRepository _matchDays;
    private readonly IFixtureRepository _fixtures;
    private readonly FinanceService _financeService;

    public TrainingService(
        IPlayerRepository players,
        ISeasonRepository seasons,
        IUnitOfWork unitOfWork,
        IClock clock,
        ITrainingSessionRepository sessions,
        IMatchDayRepository matchDays,
        IFixtureRepository fixtures,
        FinanceService financeService)
    {
        _players = players;
        _seasons = seasons;
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

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var played = await TheAllowanceForAsync(teamId, season.Id, today, cancellationToken);

        var quotes = new SquadTrainingQuotes
        {
            TeamId = teamId,
            SeasonId = season.Id,
            SquadEnergy = states.Sum(state => state.Energy),
            Day = today,
            PlaysToday = played.Plays,
            SessionsAllowed = played.Allowed,
            SessionsSpent = played.Spent,
            Players = squad
                .OrderBy(player => player.Position)
                .ThenByDescending(player => player.Potential)
                .Select(player => QuoteFor(player, stateByPlayer[player.Id]))
                .ToList()
        };

        return quotes;
    }

    private static TrainingQuote QuoteFor(Player player, PlayerSeasonState state)
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
            SessionFee = TrainingRules.SessionFee(PlayerValuation.SeasonWage(player, state))
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
    /// A club's training allowance for one day: whether it is playing, how many sessions it
    /// has, how many are gone, and the matchday the day is, if the calendar has one.
    /// </summary>
    /// <param name="Plays">Whether the club has a fixture on the day.</param>
    /// <param name="Allowed">The sessions the day is worth.</param>
    /// <param name="Spent">The sessions already run.</param>
    /// <param name="MatchDay">The calendar's day, when there is one.</param>
    private sealed record Allowance(bool Plays, int Allowed, int Spent, MatchDay? MatchDay)
    {
        /// <summary>What is left to spend, never below zero so a screen can print it as it is.</summary>
        public int Left => Math.Max(0, Allowed - Spent);
    }

    /// <summary>
    /// The day's allowance, counted from the sessions rather than from a tally.
    ///
    /// <para>
    /// Two set reads and a count between them: the club's fixtures on the day, and the club's
    /// sessions on the day. The calendar is only opened to name the day a fee is written
    /// against, which is the same reason every line of the club's book carries a matchday
    /// number — and the number is null on a day the calendar has no day for, rather than
    /// invented so that a statement always has a day in it.
    /// </para>
    /// </summary>
    private async Task<Allowance> TheAllowanceForAsync(
        Guid teamId,
        Guid seasonId,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var matchDay = (await _matchDays.ListBySeasonAsync(seasonId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Date == day);

        var fixtures = await _fixtures.ListByTeamAndDateAsync(teamId, day, cancellationToken);
        var plays = fixtures.Count > 0;

        var spent = (await _sessions.ListByTeamAndDayAsync(teamId, day, cancellationToken)).Count;

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
        var allowance = await TheAllowanceForAsync(teamId, season.Id, today, cancellationToken);

        // The refusal comes before the price is quoted, because a manager who has spent the
        // day's allowance is not being offered anything and should not be shown what it would
        // have cost first. The count is the sessions themselves, so this cannot disagree with
        // the history it is counting.
        if (allowance.Left <= 0)
        {
            throw new DomainValidationException(
                "TrainingAllowanceSpent",
                allowance.Plays
                    ? $"O clube já usou as {TrainingRules.SessionsOnAMatchDay} sessão " +
                      $"de hoje e joga hoje."
                    : $"O clube já usou as {TrainingRules.SessionsOnARestDay} sessões de hoje.");
        }

        var cost = TrainingRules.Cost(player, attribute);

        if (state.Energy < cost)
        {
            throw new DomainValidationException(
                "NotEnoughEnergy",
                $"{player.Name} precisa de {cost} de energia para treinar {Describe(attribute)} " +
                $"e tem {state.Energy}.");
        }

        var fee = TrainingRules.SessionFee(PlayerValuation.SeasonWage(player, state));
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

        // Reported after the commit and not before, so the box cannot announce a payment that
        // then failed to be written — and a failure to report is not a failure to train. The
        // session is spent, the point is gained and the money is gone by this point; raising
        // the request over a notification would hand the manager an error for work that
        // succeeded, and the retry that follows would spend a second session and a second fee
        // on a man who had already had one.
        try
        {
            await _financeService.ReportAsync(line, cancellationToken: cancellationToken);
        }
        catch (Exception reportFailed)
        {
            Debug.WriteLine(
                $"Treino de {player.Id} committed; the report to the box failed: {reportFailed}");
        }

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
