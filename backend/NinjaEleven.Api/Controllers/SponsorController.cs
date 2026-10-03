using Microsoft.AspNetCore.Mvc;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Services;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// Shirt deals: the sponsor on the shirt, the offers waiting, and the deal the manager
/// signs. Routes are singular, as the architecture requires.
/// </summary>
[ApiController]
[Route("team")]
[Produces("application/json")]
public class SponsorController : ControllerBase
{
    private readonly SponsorOfferService _sponsorService;

    public SponsorController(SponsorOfferService sponsorService)
    {
        _sponsorService = sponsorService;
    }

    /// <summary>
    /// The sponsor book of a club for a season.
    ///
    /// The season is queried because the same club can be in different seasons and the book
    /// is kept per season: a deal signed in one season does not pay in the next.
    /// </summary>
    [HttpGet("{teamId:guid}/sponsor")]
    public async Task<ActionResult<SponsorBookDto>> GetBook(
        Guid teamId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var book = await _sponsorService.GetBookAsync(teamId, seasonId, cancellationToken);

        return Ok(ToDto(book));
    }

    /// <summary>
    /// Signs a shirt deal for a club.
    ///
    /// A club cannot sign a new deal while its current one is still active: the deal has to
    /// run out first, which is enforced by the service. The response is the refreshed book
    /// so the screen can show the new sponsor without a second call.
    /// </summary>
    [HttpPost("{teamId:guid}/sponsor/sign")]
    public async Task<ActionResult<SponsorBookDto>> Sign(
        Guid teamId,
        [FromQuery] Guid seasonId,
        [FromBody] SponsorSignRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var book = await _sponsorService.SignAsync(
            teamId,
            seasonId,
            request.SponsorId,
            cancellationToken);

        return Ok(ToDto(book));
    }

    private static SponsorOfferDto OfferToDto(SponsorOffer offer)
    {
        return new SponsorOfferDto
        {
            Id = offer.SponsorId,
            Name = offer.Name,
            Industry = offer.Industry,
            PerMatchFee = offer.PerMatchFee,
            ContractMatches = offer.ContractMatches,
            Color = offer.Color,
            Logo = ApiMapper.LogoToDto(offer.Name, offer.Color),
            Weight = offer.Weight,
            ClubsSponsored = offer.ClubsSponsored,
            MaxClubs = offer.MaxClubs
        };
    }

    /// <summary>
    /// Adapts the application model to the wire shape the frontend was designed for.
    /// </summary>
    /// <remarks>
    /// The current deal is surfaced as a SponsorOfferDto (the same shape as the candidates)
    /// plus a separate MatchesLeft, so a screen that knows how to draw one offer can draw
    /// the master sponsor the same way it draws any candidate waiting in the wings.
    /// </remarks>
    private static SponsorBookDto ToDto(SponsorBook book)
    {
        var current = book.Current;
        var currentOffer = current is null ? null : new SponsorOfferDto
        {
            Id = current.SponsorId,
            Name = current.SponsorName,
            Industry = current.SponsorIndustry,
            PerMatchFee = current.PerMatchFee,
            ContractMatches = current.ContractMatches,
            Color = current.SponsorColor,
            Logo = ApiMapper.LogoToDto(current.SponsorName, current.SponsorColor)
        };

        return new SponsorBookDto
        {
            TeamId = book.TeamId,
            SeasonId = book.SeasonId,
            Current = currentOffer,
            MatchesLeft = current?.MatchesLeft ?? 0,
            Candidates = book.Candidates.Select(OfferToDto).ToList(),
            MasterSponsorId = current?.SponsorId ?? Guid.Empty
        };
    }
}
