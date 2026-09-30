using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Players;

/// <summary>
/// One session of training, kept rather than counted.
///
/// <para>
/// This row exists for two reasons that both make it worth its own table. The first is the
/// club's daily allowance, which is read by counting this club's rows for the day: a counter
/// that had to be reset, incremented and trusted would be a second source of truth about a
/// number the sessions themselves already carry, and a counter that drifted would silently
/// hand a manager a session that should have been refused. The second is the fee, which is a
/// line in the club's book that has to name the session that caused it — a manager who has
/// been charged fifteen per cent of a striker's wage is entitled to be told which morning of
/// which matchday bought the extra point.
/// </para>
///
/// <para>
/// It is history, not a cache. Nothing is ever removed from it and nothing is ever corrected
/// in it: a session that happened happened, and the day it happened on is the day the budget
/// was spent against. That is what makes the allowance a fact rather than a claim.
/// </para>
/// </summary>
public class TrainingSession
{
    private TrainingSession() { }

    /// <summary>What this session was.</summary>
    public Guid Id { get; private set; }

    /// <summary>Who was put through it.</summary>
    public Guid PlayerId { get; private set; }

    /// <summary>
    /// The club that paid for it. The allowance is the club's and not the player's: a squad
    /// of twenty-three is one man's development budget, so training your third-choice
    /// centre-back twice on a rest day spends the same session you spent on your striker.
    /// </summary>
    public Guid TeamId { get; private set; }

    /// <summary>The season the session was spent in.</summary>
    public Guid SeasonId { get; private set; }

    /// <summary>
    /// The calendar day the session belongs to. The whole allowance turns on this value and
    /// not on the instant: a club that trained at 23:59 and again at 00:01 has had a match
    /// day and a rest day, and the two sessions are two days' football rather than one.
    /// </summary>
    public DateOnly Day { get; private set; }

    /// <summary>When it was run, for the manager who wants to know when.</summary>
    public DateTimeOffset PerformedAt { get; private set; }

    /// <summary>
    /// The matchday the session was spent on, or null for a day the calendar has no matchday
    /// for. It is kept so a session can be shown against the calendar rather than against a
    /// wall clock, which is the same reason every line of the club's book carries a day.
    /// </summary>
    public Guid? MatchDayId { get; private set; }

    /// <summary>What was worked on.</summary>
    public PlayerAttribute Attribute { get; private set; }

    /// <summary>What the session cost the man in energy.</summary>
    public int EnergyCost { get; private set; }

    /// <summary>
    /// What it cost the club in money, being <see cref="TrainingRules.SessionFeeRate"/> of
    /// the man's season wage as it stood on the day. It is stored rather than recomputed
    /// because a wage is a function of a man's age, a season and his season's injuries, and
    /// all three move under the row: a fee that were re-derived later would be a number that
    /// changes under a manager who is looking at a statement he already holds.
    /// </summary>
    public decimal Fee { get; private set; }

    /// <summary>
    /// Which session of the day this was, counted from zero.
    /// </summary>
    /// <remarks>
    /// The allowance is counted by counting rows, and a count is only a fact until the
    /// database refuses to be out of step with it. Two sessions arriving at once would both
    /// read an allowance of one and both be told yes; this column carries a unique index over
    /// the club and the day with it, so the second cannot be written whatever the read said.
    /// The service still counts first, because it has a good error code and the constraint
    /// does not — but the rule is the last line of defence rather than the only one, and the
    /// failure when it fires is the safe one: the whole command fails, so no energy is spent
    /// and no fee is charged.
    /// </remarks>
    public int Ordinal { get; private set; }

    /// <summary>
    /// Records a session that has been paid for, worked and charged, in one immutable act.
    /// </summary>
    /// <param name="playerId">The man who trained.</param>
    /// <param name="teamId">The club whose allowance this session spent.</param>
    /// <param name="seasonId">The season it was spent in.</param>
    /// <param name="day">The calendar day it belongs to.</param>
    /// <param name="performedAt">The instant it ran.</param>
    /// <param name="matchDayId">The matchday it falls on, when the calendar has one.</param>
    /// <param name="attribute">What was worked on.</param>
    /// <param name="energyCost">The energy the man spent.</param>
    /// <param name="fee">What the club paid.</param>
    /// <param name="ordinal">Which session of the day this is, from zero.</param>
    public static TrainingSession Create(
        Guid playerId,
        Guid teamId,
        Guid seasonId,
        DateOnly day,
        DateTimeOffset performedAt,
        Guid? matchDayId,
        PlayerAttribute attribute,
        int energyCost,
        decimal fee,
        int ordinal = 0)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("A session needs a man to have happened to.", nameof(playerId));
        }

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("A session needs a club to have been paid for.", nameof(teamId));
        }

        if (energyCost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(energyCost), energyCost, "A session that cost nothing is not a session.");
        }

        if (fee < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fee), fee, "Nobody is paid to train a man out of the club's account.");
        }

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal), ordinal, "A session is the first of the day or it is not the first.");
        }

        return new TrainingSession
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            TeamId = teamId,
            SeasonId = seasonId,
            Day = day,
            PerformedAt = performedAt,
            MatchDayId = matchDayId,
            Attribute = attribute,
            EnergyCost = energyCost,
            Fee = fee,
            Ordinal = ordinal
        };
    }
}
