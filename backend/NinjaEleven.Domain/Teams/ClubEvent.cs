namespace NinjaEleven.Domain.Teams;

/// <summary>
/// The moments of a club's life that nothing else in the world remembers.
///
/// <para>
/// The world is very good at remembering results and very bad at remembering decisions. A
/// title is a row in <c>trophy_awards</c> and a relegation is a tier that is not the one it
/// was — but "the club was called Náutico de Guanabara until the third season" and "the
/// manager drew a new crest in the winter" leave no trace at all, because <see cref="Team"/>
/// keeps one name column and one crest column and each overwrite is the end of the story.
/// A club's page has nothing to read for those moments, and the honest answer is that the
/// game never wrote them down.
/// </para>
///
/// <para>
/// So they are written down here, at the moment they happen, by the same call that performs
/// them. The row carries the fact and not the sentence: the values before and after are the
/// data, and the words a page builds out of them are that page's own. A reworded line
/// changes nothing that was recorded.
/// </para>
///
/// <para>
/// Only the moments with a writer live here. A promotion, a title and a division's top
/// scorer are facts the tables already hold and are derived when they are asked for rather
/// than copied into a second table that could disagree with the first — so
/// <see cref="Kind"/> carries the three that had to be recorded, and not the five that did
/// not need to be.
/// </para>
/// </summary>
public enum ClubEventKind
{
    /// <summary>The club was renamed by the manager running it.</summary>
    NameChange = 1,

    /// <summary>The club's crest was drawn or taken away.</summary>
    CrestChange = 2,

    /// <summary>The club's two colours were changed.</summary>
    ColorsChange = 3
}

/// <summary>
/// One recorded moment in a club's life.
/// </summary>
public class ClubEvent
{
    public Guid Id { get; private set; }

    public Guid TeamId { get; private set; }

    /// <summary>
    /// The season it happened in, when there was one running.
    ///
    /// <para>
    /// Null rather than required because a manager renames a club between seasons too, and a
    /// row that refused to be written outside a season would lose exactly the moments a
    /// manager is most likely to be sitting still for. The page dates the row from
    /// <see cref="OccurredAt"/> when the season is unknown rather than inventing one.
    /// </para>
    /// </summary>
    public Guid? SeasonId { get; private set; }

    public ClubEventKind Kind { get; private set; }

    /// <summary>
    /// What the club was called, wore or was painted with before, as text.
    ///
    /// <para>
    /// Null for a moment that has no text on either side of it — a crest is a drawing, and
    /// "the crest was this and then it was that" is not a sentence. It is here for the moment
    /// that has one, which is a rename: "Náutico de Guanabara became Náutico" is the whole of
    /// what happened and a page should not have to be told it in prose to be able to say it.
    /// </para>
    /// </summary>
    public string? PreviousValue { get; private set; }

    /// <summary>What it is after, on the same terms as <see cref="PreviousValue"/>.</summary>
    public string? NewValue { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private ClubEvent() { }

    public static ClubEvent Create(
        Guid teamId,
        ClubEventKind kind,
        Guid? seasonId = null,
        string? previousValue = null,
        string? newValue = null)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("A club event belongs to a club.", nameof(teamId));
        }

        return new ClubEvent
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Kind = kind,
            SeasonId = seasonId,
            PreviousValue = Blank(previousValue),
            NewValue = Blank(newValue),
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// An empty string is not an answer and a page would print it. A rename that leaves the
    /// club with no name is impossible — <see cref="Team.SetName"/> refuses it — but a colour
    /// change can be handed a blank by a request that was not read carefully, and a row that
    /// said the club used to be <c>""</c> is a row that lies in a sentence.
    /// </summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}