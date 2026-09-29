namespace NinjaEleven.Domain.Inbox;

/// <summary>
/// One thing the game has to tell the manager, delivered to his club's box.
///
/// The message is **not** a document the manager writes. Nothing in the domain accepts a
/// message as input and no endpoint takes one: the only way a row gets here is a piece of the
/// engine deciding that something has happened to this club. A box a user can type into is a
/// mailbox, and a mailbox in a football game is a second game with no rules behind it — so
/// reading is the whole of the feature and writing belongs to the world.
///
/// **The body is written the way a newspaper writes.** Money is reported the way a paper
/// reports a signing, and a result is written up the way a page writes up a match: a headline
/// that reads on its own, then the facts underneath it. A box of forty lines saying
/// "Salário -120.000" is a spreadsheet the manager has already seen on the finance screen,
/// and a line he skims taught him nothing; the same line as a paragraph is the one he opens.
///
/// **The body carries no markup of its own.** Paragraphs are separated by a blank line and
/// everything else is plain text, because a body that could carry HTML would be a place where
/// a club's name stops being a name — and a name that is not a link is a name a manager
/// cannot follow. The names a message uses travel beside it in <see cref="Mentions"/>, and
/// the client turns them into doors with the rule it already uses on the match feed: a name
/// only becomes a link when exactly one thing in the world answers to it.
///
/// **A message is a fact, so it is written once.** The <see cref="Reference"/> is the guard,
/// in the same shape the ledger uses: a gate is `match:{matchId}:gate`, a result is
/// `match:{matchId}:result`, a prize borrows the reference the prize was paid under. A
/// process that was down over a weekend and restarted does not tell a club its bank balance
/// twice because the match that paid it was settled again.
/// </summary>
public class InboxMessage
{
    public Guid Id { get; private set; }

    /// <summary>
    /// The club the message was delivered to.
    ///
    /// A club, and not a user. A club has one manager, so the two are the same question with
    /// a different answer for nobody, and the club is what the rest of the game already
    /// identifies a career by. It is also what makes the rule about the computer-controlled
    /// clubs possible: a club nobody is running has no one to deliver to, so it is never sent
    /// a message and never holds one.
    /// </summary>
    public Guid RecipientTeamId { get; private set; }

    public InboxCategory Category { get; private set; }

    /// <summary>
    /// The subject line. On a narrow column it is the whole message, so it carries the club's
    /// name and the number of the day rather than a bare noun.
    /// </summary>
    public string Subject { get; private set; }

    /// <summary>Who it is from, in the words a manager would use for them.</summary>
    public string SenderName { get; private set; }

    /// <summary>
    /// The text of the message. Paragraphs are separated by a blank line and there is no other
    /// structure: the client splits on the blank line and renders what it is given.
    /// </summary>
    public string Body { get; private set; }

    /// <summary>
    /// The names the body uses, packed into one document by <see cref="InboxMentions"/>.
    /// </summary>
    public string Mentions { get; private set; } = "[]";

    /// <summary>
    /// The one door out of the message, when there is somewhere to go.
    ///
    /// A route the client understands rather than a URL, so a message can never point the
    /// manager at a page of the game that does not exist. The wage bill is a number and not a
    /// place, so it carries no label and no route.
    /// </summary>
    public string? LinkLabel { get; private set; }

    public string? LinkRoute { get; private set; }

    /// <summary>
    /// What this message is the report of, and the guard against sending it twice.
    /// </summary>
    public string Reference { get; private set; }

    /// <summary>When the engine said it, which is when the manager received it.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    private InboxMessage() { }

    public static InboxMessage Create(
        Guid recipientTeamId,
        InboxCategory category,
        string subject,
        string senderName,
        string body,
        string reference,
        string? linkLabel = null,
        string? linkRoute = null,
        IEnumerable<InboxMention>? mentions = null,
        DateTimeOffset? createdAt = null)
    {
        if (recipientTeamId == Guid.Empty)
        {
            throw new ArgumentException("A message is delivered to a club.", nameof(recipientTeamId));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("A message has a subject line.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(senderName))
        {
            throw new ArgumentException("A message says who it is from.", nameof(senderName));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A message has something to say.", nameof(body));
        }

        // The reference is the only thing standing between a restart and a club told the same
        // news twice, so an unnamed message is refused rather than written: a box that can
        // hold a message nobody can deduplicate is a box that fills itself with echoes.
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "A message says what it is the report of, or it cannot be told from its twin.",
                nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(linkLabel) != string.IsNullOrWhiteSpace(linkRoute))
        {
            throw new ArgumentException(
                "A door out of a message is a label and a route together.",
                nameof(linkRoute));
        }

        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            RecipientTeamId = recipientTeamId,
            Category = category,
            Subject = subject.Trim(),
            SenderName = senderName.Trim(),
            Body = body.Trim(),
            Mentions = InboxMentions.Serialize(mentions),
            LinkLabel = string.IsNullOrWhiteSpace(linkLabel) ? null : linkLabel.Trim(),
            LinkRoute = string.IsNullOrWhiteSpace(linkRoute) ? null : linkRoute.Trim(),
            Reference = reference.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            IsRead = false,
            ReadAt = null
        };
    }

    /// <summary>
    /// Marks the message as read, once.
    ///
    /// Reading it twice is not a thing that happens, and a second call leaves the first time
    /// alone: the column's "read at" is the moment the manager actually opened it, and
    /// overwriting it with the moment he reopened it would be a lie about when he read it.
    /// </summary>
    public void MarkRead(DateTimeOffset? at = null)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = at ?? DateTimeOffset.UtcNow;
    }
}
