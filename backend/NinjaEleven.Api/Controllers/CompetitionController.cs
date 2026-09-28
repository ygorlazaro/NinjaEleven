using NinjaEleven.Api.Contracts;
using NinjaEleven.Api.Mappings;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace NinjaEleven.Api.Controllers;

[ApiController]
[Route("competition")]
[Produces("application/json")]
public class CompetitionController : ControllerBase
{
    private readonly CompetitionService _competitionService;
    private readonly CupBracketService _cupBracketService;
    private readonly ScorerPrizeService _scorerPrizeService;

    public CompetitionController(
        CompetitionService competitionService,
        CupBracketService cupBracketService,
        ScorerPrizeService scorerPrizeService)
    {
        _competitionService = competitionService;
        _cupBracketService = cupBracketService;
        _scorerPrizeService = scorerPrizeService;
    }

    /// <summary>
    /// The cup's bracket: the rounds that have been drawn, and inside each tie the two clubs
    /// with both legs and the aggregate.
    ///
    /// The round after a tie is decided does not exist yet, and that is the point: a bracket that
    /// drew the quarter-finals before the round of sixteen was played would be showing two clubs
    /// in them that neither has earned. The aggregate is sent per club, because the two legs swap
    /// ends and a client that added the scores as they arrived would read the tie the wrong way
    /// round on exactly the ties that were close.
    /// </summary>
    [HttpGet("{competitionSeasonId:guid}/bracket")]
    public async Task<ActionResult<CupBracketDto>> GetBracket(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var bracket = await _cupBracketService.GetAsync(competitionSeasonId, cancellationToken);
        if (bracket is null)
        {
            throw new EntityNotFoundException(nameof(CompetitionSeason), competitionSeasonId);
        }

        return Ok(bracket.ToDto());
    }

    /// <summary>
    /// What one edition pays its artilharia, and who is holding the three places.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edition is the address rather than the season, because a season's championship is
    /// three editions: one artilharia per division, each paid a share of its own division's
    /// title, and a season-wide answer would be a list of the whole country's top scorers with
    /// no way to say which title pays which of them.
    /// </para>
    /// <para>
    /// A cup is one edition and has no purse of its own, so each of its three scorers is paid a
    /// share of the title of the division his club is in, and each line says whose title that
    /// was. The shares come back whether or not anybody has scored yet: a legend of a prize that
    /// has not been won is still the rule a manager is reading.
    /// </para>
    /// </remarks>
    [HttpGet("{competitionSeasonId:guid}/top-scorer-prize")]
    public async Task<ActionResult<TopScorerPrizeListDto>> GetTopScorerPrize(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var prizes = await _scorerPrizeService.GetAsync(competitionSeasonId, cancellationToken);
        return Ok(prizes.ToDto());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompetitionDto>>> List(CancellationToken cancellationToken)
    {
        var competitions = await _competitionService.GetAllAsync(cancellationToken);
        return Ok(competitions.ToDtos());
    }

    /// <summary>
    /// What the cup pays: the winner's cheque and the consolation for the round a club went out in.
    ///
    /// A knockout is paid on the way out, so a screen that listed the winner and forgot the
    /// loser would tell a manager what his club is playing for and not what it is playing
    /// against. The consolation grows steeply as the round does, and the difference between the
    /// first-round loser and the finalist is the prize for having been in the competition at all.
    /// </summary>
    [HttpGet("cup-prizes")]
    public ActionResult<IReadOnlyList<CupPrizeDto>> GetCupPrizes() =>
        Ok(_competitionService.GetCupPrizes().ToDtos());

    /// <summary>
    /// The clubs entered in one edition of a competition.
    ///
    /// This is the list the club picker is built from, and it is asked of the backend because
    /// a club's division is a fact about its enrolment in an edition rather than a column on
    /// the club: a client handed a list of clubs and a list of divisions has to join them up
    /// itself, and joining them up itself is how a club ends up in the 3ª Divisão because the
    /// two lists were sorted differently.
    /// </summary>
    [HttpGet("{competitionSeasonId:guid}/club")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> ListClubs(
        Guid competitionSeasonId,
        CancellationToken cancellationToken)
    {
        var clubs = await _competitionService.GetClubsAsync(competitionSeasonId, cancellationToken);
        return Ok(clubs.ToDtos());
    }

    [HttpGet("by-season/{seasonId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CompetitionEditionDto>>> ListBySeason(
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        var competitions = await _competitionService.GetBySeasonAsync(seasonId, cancellationToken);
        return Ok(competitions.ToDtos());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompetitionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var competition = await _competitionService.GetByIdAsync(id, cancellationToken);
        return Ok(competition.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionDto>> Create(
        [FromBody] CreateCompetitionRequestDto request,
        CancellationToken cancellationToken)
    {
        var competition = await _competitionService.CreateAsync(request.Name, request.Type, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = competition.Id }, competition.ToDto());
    }
}
