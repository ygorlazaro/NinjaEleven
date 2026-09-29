using NinjaEleven.Domain.Inbox;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The rules a message has to keep whatever the engine is doing when it writes one.
/// </summary>
public class InboxMessageTests
{
    private static InboxMessage AMessage(string reference = "finance:abc") =>
        InboxMessage.Create(
            Guid.NewGuid(),
            InboxCategory.Finance,
            "Ninja Eleven em alta",
            "Tesouraria do Ninja Eleven",
            "O caixa do Ninja Eleven ganhou L$ 24.000.",
            reference,
            linkLabel: "Ver o extrato",
            linkRoute: "/financeiro");

    [Fact]
    public void ANewMessageArrivesUnread()
    {
        var message = AMessage();

        Assert.False(message.IsRead);
        Assert.Null(message.ReadAt);
    }

    /// <summary>
    /// The reference is the only thing standing between a restart and a club told the same
    /// news twice, so a message that cannot be told from its twin is refused rather than
    /// written: a box that can hold one is a box that fills itself with echoes.
    /// </summary>
    [Fact]
    public void AMessageWithoutAReferenceIsRefused()
    {
        Assert.Throws<ArgumentException>(() => InboxMessage.Create(
            Guid.NewGuid(),
            InboxCategory.Finance,
            "Assunto",
            "Remetente",
            "Conteúdo",
            reference: "   "));
    }

    /// <summary>
    /// A door is a label and a route together. A label with nowhere to go and a route with
    /// nothing written on it are both a control on a screen that does nothing when pressed.
    /// </summary>
    [Fact]
    public void ADoorOutOfAMessageIsALabelAndARouteTogether()
    {
        Assert.Throws<ArgumentException>(() => InboxMessage.Create(
            Guid.NewGuid(),
            InboxCategory.Finance,
            "Assunto",
            "Remetente",
            "Conteúdo",
            "finance:abc",
            linkLabel: "Ver o extrato"));

        Assert.Throws<ArgumentException>(() => InboxMessage.Create(
            Guid.NewGuid(),
            InboxCategory.Finance,
            "Assunto",
            "Remetente",
            "Conteúdo",
            "finance:abc",
            linkRoute: "/financeiro"));
    }

    /// <summary>
    /// Reading it twice is not a thing that happens, and a second call leaves the first time
    /// alone. "Read at" is when the manager read it; overwriting it with the moment he
    /// reopened it would be a lie about when he read it.
    /// </summary>
    [Fact]
    public void ReadingAMessageTwiceKeepsTheFirstMoment()
    {
        var message = AMessage();
        var first = DateTimeOffset.UtcNow.AddHours(-1);

        message.MarkRead(first);
        message.MarkRead();

        Assert.True(message.IsRead);
        Assert.Equal(first, message.ReadAt);
    }

    /// <summary>
    /// The names travel with the message rather than being worked out of its text, and a name
    /// said twice is one name: two entries for one club would make the client treat a name it
    /// could have linked as a name two things answer to, and refuse to link it at all.
    /// </summary>
    [Fact]
    public void TheNamesOfAMessageArePackedWithoutRepeats()
    {
        var id = Guid.NewGuid();
        var message = InboxMessage.Create(
            Guid.NewGuid(),
            InboxCategory.MatchReport,
            "Assunto",
            "Redação",
            "Ninja Eleven venceu o Flamengo.",
            "match:1:result",
            mentions:
            [
                new InboxMention { Name = "Ninja Eleven", Kind = InboxMentionKind.Team, EntityId = id },
                new InboxMention { Name = "ninja eleven", Kind = InboxMentionKind.Team, EntityId = id },
                new InboxMention { Name = "  ", Kind = InboxMentionKind.Team, EntityId = id }
            ]);

        var mentions = InboxMentions.Parse(message.Mentions);

        Assert.Single(mentions);
        Assert.Equal("Ninja Eleven", mentions[0].Name);
    }

    /// <summary>
    /// A document that will not parse is a message whose links are lost, not a message to be
    /// refused: the text is the message, and plain names beat an error page.
    /// </summary>
    [Fact]
    public void AMentionDocumentThatWillNotParseCostsTheLinksAndNotTheMessage()
    {
        var mentions = InboxMentions.Parse("{isto nao e um documento}");

        Assert.Empty(mentions);
    }
}
