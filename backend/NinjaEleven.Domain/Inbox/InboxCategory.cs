namespace NinjaEleven.Domain.Inbox;

/// <summary>
/// What a message is about, because a box with fifty lines in it and no way to tell a goal
/// from a payslip is a box a manager learns to skip.
///
/// The mark is not decoration and it is not a filter the client is free to invent: it is
/// stored with the message, so the column's badge, the screen's grouping and any future
/// "only the news" view are all reading the same fact rather than parsing the subject to
/// guess which of them it is.
/// </summary>
public enum InboxCategory
{
    /// <summary>A line of the club's book: the gate, the wages, a sponsor, a transfer.</summary>
    Finance,

    /// <summary>How a match the club played ended, written up.</summary>
    MatchReport,

    /// <summary>Somebody wants one of the club's players.</summary>
    TransferOffer,

    /// <summary>A championship, a cup or an artilharia the club has won.</summary>
    Title,

    /// <summary>
    /// A cup round the country played, told to every manager rather than to the one club it
    /// happened to involve. It is separate from <see cref="Title"/> because the news is not a
    /// prize: nobody won anything, a set of clubs went through and another set did not, and a
    /// box that filed that under titles would be filing an elimination next to a championship.
    /// </summary>
    CupRound,

    /// <summary>
    /// A season that is over: its four final tables and who moved between them.
    /// </summary>
    SeasonSummary,

    /// <summary>
    /// A matchday of the championship, told to every manager: who won, and who moved.
    ///
    /// <para>
    /// It is a mark of its own rather than a corner of <see cref="SeasonSummary"/>, because a
    /// matchday decides nothing and awards nothing. A season's end moves four tables and settles
    /// who the champions are; this is the other thing — a Wednesday in October, thirty-two
    /// results, and a table that shuffles underneath it. Filed under a season summary, a manager
    /// would have to open the same mark to find out which of its lines is the end of the year and
    /// which is last Tuesday.
    /// </para>
    /// </summary>
    RoundSummary,

    /// <summary>Anything the game itself has to say that is none of the above.</summary>
    Club
}

/// <summary>
/// One kind of message and how many of them the box holds.
///
/// It is a domain fact rather than a row of a filter: "how much of my mail is this" is a
/// question about the box, and a screen that answered it by counting the twenty lines it
/// happened to be holding would be answering it about the page instead.
/// </summary>
public readonly record struct InboxCategoryTally(InboxCategory Category, int Count);
