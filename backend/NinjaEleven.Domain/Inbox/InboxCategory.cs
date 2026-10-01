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

    /// <summary>Anything the game itself has to say that is none of the above.</summary>
    Club
}
