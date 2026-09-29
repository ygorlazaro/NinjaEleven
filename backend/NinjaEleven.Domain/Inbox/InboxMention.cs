using System.Text.Json;
using System.Text.Json.Serialization;

namespace NinjaEleven.Domain.Inbox;

/// <summary>
/// A name a message uses, and the thing that name belongs to.
///
/// A club's short name and its full name are two entries, and the client matches the longest
/// first, so "Flamengo" inside "Flamengo da Floresta" is claimed as the club and the rest of
/// the sentence is left alone — the same rule the match feed already lives by.
/// </summary>
public sealed class InboxMention
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the name opens a player or a club.</summary>
    public string Kind { get; set; } = InboxMentionKind.Team;

    public Guid EntityId { get; set; }
}

/// <summary>The two kinds of thing a name in a message can be a door to.</summary>
public static class InboxMentionKind
{
    public const string Player = "player";
    public const string Team = "team";
}

/// <summary>
/// The names of a message, and the reason they are carried as one string rather than as rows.
///
/// The shape belongs to whoever draws the screen, exactly as it does for a player's face: the
/// backend stores the document and does not model it twice. It is stored rather than drawn on
/// the spot for the same reason the face is — the same message is read again and again, and a
/// box that showed a manager a different man each time he opened the same line would be a box
/// he could not trust.
/// </summary>
public static class InboxMentions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Packs the people a message talks about, dropping the repeats and keeping the first
    /// mention of each name. Two entries for one club would make the client treat a name it
    /// could have linked as a name two things answer to, and refuse to link it at all.
    /// </summary>
    public static string Serialize(IEnumerable<InboxMention>? mentions)
    {
        if (mentions is null)
        {
            return "[]";
        }

        var distinct = new List<InboxMention>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mention in mentions)
        {
            if (string.IsNullOrWhiteSpace(mention.Name) || mention.EntityId == Guid.Empty)
            {
                continue;
            }

            if (seen.Add(mention.Name.Trim()))
            {
                distinct.Add(new InboxMention
                {
                    Name = mention.Name.Trim(),
                    Kind = string.IsNullOrWhiteSpace(mention.Kind) ? InboxMentionKind.Team : mention.Kind,
                    EntityId = mention.EntityId
                });
            }
        }

        return JsonSerializer.Serialize(distinct, Options);
    }

    /// <summary>
    /// Unpacks the names. A document that will not parse is a message whose links are lost,
    /// not a message that has to be refused: the text is the message, and a manager is better
    /// served by plain names than by an error page.
    /// </summary>
    public static IReadOnlyList<InboxMention> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<InboxMention>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<InboxMention>>(json, Options)
                   ?? (IReadOnlyList<InboxMention>)Array.Empty<InboxMention>();
        }
        catch (JsonException)
        {
            return Array.Empty<InboxMention>();
        }
    }
}
