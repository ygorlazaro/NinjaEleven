using Microsoft.AspNetCore.Mvc;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// The manager's box, read and marked read.
///
/// It is a read-only resource in every sense that matters: there is no endpoint that takes a
/// message, and nothing here composes one. The only verbs are reading a page, opening a
/// message, catching up on all of them, and asking how many are new — because the messages
/// themselves are written by the engine, and a manager who could type into his own box would
/// be writing into a place the game's facts are supposed to live.
///
/// The club is in the route rather than in a body or a query because that is where the rest
/// of the game puts it, and because the rule about who has a box is the club's: a club
/// without a manager has an empty one, and it is asked of the database rather than of the
/// client.
/// </summary>
[ApiController]
[Route("inbox")]
[Produces("application/json")]
public class InboxController : ControllerBase
{
    private readonly InboxService _inbox;

    public InboxController(InboxService inbox) => _inbox = inbox;

    /// <summary>A page of a club's box, newest first, with the number of unread messages.</summary>
    [HttpGet("{teamId:guid}")]
    public async Task<ActionResult<InboxBoxDto>> GetBox(
        Guid teamId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var box = await _inbox.GetBoxAsync(teamId, page, pageSize, cancellationToken);
        return Ok(box.ToDto());
    }

    /// <summary>How many messages the manager has not opened. The number on the column.</summary>
    [HttpGet("{teamId:guid}/unread")]
    public async Task<ActionResult<int>> GetUnreadCount(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        Ok(await _inbox.GetUnreadCountAsync(teamId, cancellationToken));

    /// <summary>
    /// Opens a message.
    ///
    /// A message is read by the club that owns it and by nobody else, so a manager who guessed
    /// his way to somebody else's line is told the line is not his — the same answer the
    /// transfer inbox gives, and the only honest one: there is no such thing as a global box.
    /// </summary>
    [HttpPost("{teamId:guid}/read/{messageId:guid}")]
    public async Task<ActionResult<InboxMessageDto>> MarkRead(
        Guid teamId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _inbox.MarkReadAsync(teamId, messageId, cancellationToken);

        if (message is null)
        {
            throw new EntityNotFoundException("InboxMessage", messageId);
        }

        return Ok(message.ToDto());
    }

    /// <summary>Empties the unread badge in one go, for a manager who has caught up.</summary>
    [HttpPost("{teamId:guid}/read-all")]
    public async Task<ActionResult<int>> MarkAllRead(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        Ok(await _inbox.MarkAllReadAsync(teamId, cancellationToken));
}
