namespace NinjaEleven.Domain.Matches;

/// <summary>
/// The plan a manager has laid for his club's next match: the shape he wants and the men he
/// wants to play it in.
///
/// <para>
/// It exists because the kick-off is no longer the manager's. A match is started by the
/// world's own schedule rather than by the act of opening a screen, so a manager who is not
/// there when his club kicks off still gets his club out on the pitch in the way he asked,
/// and a manager who never asked gets the last thing he did ask for rather than whatever the
/// engine's mood produces on the day.
/// </para>
///
/// <para>
/// It is one plan per club per season and not one per fixture. A plan is how this club
/// plays, and asking a manager to re-say it every eight days is asking him to manage a
/// setting rather than a team; the fixture it applies to is read from the calendar when the
/// match starts, which is the only place a fixture exists.
/// </para>
/// </summary>
public sealed class TeamMatchPlan
{
    private TeamMatchPlan() { }

    /// <summary>The plan's own identity.</summary>
    public Guid Id { get; private set; }

    /// <summary>The club that planned it.</summary>
    public Guid TeamId { get; private set; }

    /// <summary>The season it is planned for. A plan does not outlive the squad it was made for.</summary>
    public Guid SeasonId { get; private set; }

    /// <summary>
    /// The shape, from the <see cref="Tactics"/> catalogue. Empty means "whatever the club is
    /// made of", which is a real answer rather than the absence of one: <see cref="Tactics"/>
    /// reads the shape off the available players for exactly that case.
    /// </summary>
    public string TacticCode { get; private set; } = string.Empty;

    /// <summary>
    /// The eleven, in the manager's order.
    ///
    /// <para>
    /// It is stored as the manager set it and not repaired on the way in: a player who is
    /// suspended on the day is a fact the manager did not know, and the plan is answered
    /// against the squad as it is when the match starts rather than refused because it was
    /// written on a day when he could still pick him.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid> StarterIds { get; private set; } = [];

    /// <summary>
    /// The bench, in the manager's order. Fewer than seven is allowed and means "the rest",
    /// because a manager who names a keeper and six men has still said something.
    /// </summary>
    public IReadOnlyList<Guid> BenchIds { get; private set; } = [];

    /// <summary>When it was last said, for a manager who wants to know how old his own order is.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Whether this plan names an eleven at all, as against only a shape.</summary>
    public bool NamesAnEleven => StarterIds.Count > 0;

    /// <summary>
    /// Records the plan, replacing whatever this club had planned before for this season.
    /// </summary>
    /// <param name="teamId">The club planning.</param>
    /// <param name="seasonId">The season it is planned for.</param>
    /// <param name="tacticCode">The shape, or empty for the one read off the squad.</param>
    /// <param name="starterIds">The eleven, in the manager's order.</param>
    /// <param name="benchIds">The bench, in the manager's order.</param>
    /// <param name="updatedAt">The instant it was said.</param>
    public static TeamMatchPlan Create(
        Guid teamId,
        Guid seasonId,
        string tacticCode,
        IReadOnlyList<Guid> starterIds,
        IReadOnlyList<Guid> benchIds,
        DateTimeOffset updatedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            SeasonId = seasonId,
            TacticCode = tacticCode ?? string.Empty,
            StarterIds = starterIds,
            BenchIds = benchIds,
            UpdatedAt = updatedAt
        };

    /// <summary>
    /// Replaces this plan with a newer statement of the same thing.
    ///
    /// <para>
    /// It is the same row rather than a new one because the plan is a standing order: two rows
    /// for a club and a season would mean two answers to "how does this club play", and the
    /// one that won would be whichever the database returned first.
    /// </para>
    /// </summary>
    public void Restate(
        string tacticCode,
        IReadOnlyList<Guid> starterIds,
        IReadOnlyList<Guid> benchIds,
        DateTimeOffset updatedAt)
    {
        TacticCode = tacticCode ?? string.Empty;
        StarterIds = starterIds;
        BenchIds = benchIds;
        UpdatedAt = updatedAt;
    }
}