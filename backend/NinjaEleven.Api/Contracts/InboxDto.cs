using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Inbox;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A page of a club's box and the number of messages in it the manager has not opened.
///
/// The unread count travels with the page for the same reason the finance ledger carries its
/// totals: the two are asked about at the same moment and by the same screen, and a count
/// fetched separately can be a second out of step with the page it is supposed to describe.
/// </summary>
public class InboxBoxDto
{
    public IReadOnlyList<InboxMessageDto> Messages { get; init; } = Array.Empty<InboxMessageDto>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public int UnreadCount { get; init; }
}

/// <summary>One message, as a reader receives it.</summary>
public class InboxMessageDto
{
    public Guid Id { get; init; }

    /// <summary>The kind, as a name. The client maps it to a mark.</summary>
    public string Category { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;

    /// <summary>
    /// The text of the message, with its paragraphs separated by a blank line. It carries no
    /// markup: the names inside it are doors because of the mentions beside it, and a body
    /// that could carry HTML would be a place where a club's name stopped being a name.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    public IReadOnlyList<InboxPersonDto> Mentions { get; init; } = Array.Empty<InboxPersonDto>();

    /// <summary>The label and the route of the one door out of the message, when it has one.</summary>
    public string? LinkLabel { get; init; }

    public string? LinkRoute { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
}

/// <summary>
/// A name a message uses and who it belongs to, so the screen can make it a door without
/// having to guess which of the world's names this particular word is.
/// </summary>
public class InboxPersonDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>"player" or "team".</summary>
    public string Kind { get; init; } = string.Empty;

    public Guid Id { get; init; }
}

public static class InboxDtoMapper
{
    public static InboxBoxDto ToDto(this InboxBox box) => new()
    {
        Messages = box.Messages.Select(ToDto).ToList(),
        Page = box.Page,
        PageSize = box.PageSize,
        TotalItems = box.TotalItems,
        TotalPages = box.TotalPages,
        UnreadCount = box.UnreadCount
    };

    public static InboxMessageDto ToDto(this InboxMessageLine line) => new()
    {
        Id = line.Id,
        Category = line.Category.ToString(),
        Subject = line.Subject,
        SenderName = line.SenderName,
        Body = line.Body,
        Mentions = line.Mentions.Select(person => new InboxPersonDto
        {
            Name = person.Name,
            Kind = person.Kind,
            Id = person.Id
        }).ToList(),
        LinkLabel = line.LinkLabel,
        LinkRoute = line.LinkRoute,
        CreatedAt = line.CreatedAt,
        IsRead = line.IsRead,
        ReadAt = line.ReadAt
    };
}
