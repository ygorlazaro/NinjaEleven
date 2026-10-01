using Microsoft.AspNetCore.Mvc;
using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Services;

namespace NinjaEleven.Api.Controllers;

/// <summary>
/// The board a manager lays his club's next match out on.
///
/// <para>
/// It is a pair of routes rather than a screen that assembles its own pieces, and the reason
/// is that the kick-off reads what is written here. A board whose eleven was composed in the
/// browser would be a second answer to "who does this club pick", and the one the match
/// actually uses would be the first.
/// </para>
/// </summary>
[ApiController]
[Route("tactics")]
[Produces("application/json")]
public class TacticsController : ControllerBase
{
    private readonly TacticsService _tactics;

    public TacticsController(TacticsService tactics)
    {
        _tactics = tactics;
    }

    /// <summary>
    /// The board: the next fixture, the opponent, the squad and the order left on it.
    ///
    /// <para>
    /// The season is asked for rather than read from a header, because the plan is a fact
    /// about one season of one club. A board opened in September and left open until January
    /// is still a board for September's season, and a plan written into the wrong one would be
    /// a plan the kick-off never reads.
    /// </para>
    /// </summary>
    [HttpGet("board")]
    public async Task<ActionResult<TacticsBoardDto>> GetBoard(
        [FromQuery] Guid teamId,
        [FromQuery] Guid seasonId,
        CancellationToken cancellationToken)
    {
        var board = await _tactics.GetBoardAsync(teamId, seasonId, cancellationToken);

        return Ok(Map(board));
    }

    /// <summary>
    /// Writes the order down.
    ///
    /// <para>
    /// It is a POST and not a PUT because the thing being written is not the plan's identity
    /// but the manager's decision, and a decision is stamped with when it was taken. A plan
    /// written twice is the last decision and not the merge of two.
    /// </para>
    /// </summary>
    [HttpPost("plan")]
    public async Task<ActionResult<TacticsPlanDto>> SavePlan(
        [FromBody] SaveTacticsPlanRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await _tactics.SavePlanAsync(
            request.TeamId,
            request.SeasonId,
            request.TacticCode,
            request.StarterIds,
            request.BenchIds,
            cancellationToken);

        return Ok(Map(plan));
    }

    private static TacticsBoardDto Map(TacticsBoard board) => new()
    {
        TeamId = board.TeamId,
        TeamName = board.TeamName,
        SeasonId = board.SeasonId,
        Next = board.Next is null
            ? null
            : new TacticsNextFixtureDto
            {
                FixtureId = board.Next.FixtureId,
                RoundNumber = board.Next.RoundNumber,
                MatchDayNumber = board.Next.MatchDayNumber,
                CompetitionName = board.Next.CompetitionName,
                CompetitionType = board.Next.CompetitionType.ToString(),
                WaveOpen = board.Next.WaveOpen
            },
        Opponent = board.Opponent is null
            ? null
            : new TacticsOpponentDto
            {
                Id = board.Opponent.Id,
                Name = board.Opponent.Name,
                Rating = board.Opponent.Rating,
                IsHome = !board.Opponent.ClubIsAtHome
            },
        HeadToHead = board.HeadToHead is null
            ? null
            : new TacticsHeadToHeadDto
            {
                Played = board.HeadToHead.Played,
                Wins = board.HeadToHead.Wins,
                Draws = board.HeadToHead.Draws,
                Losses = board.HeadToHead.Losses,
                GoalsFor = board.HeadToHead.GoalsFor,
                GoalsAgainst = board.HeadToHead.GoalsAgainst,
                GoalDifference = board.HeadToHead.GoalDifference
            },
        RecentForm = board.RecentForm
            .Select(match => new TacticsRecentMatchDto
            {
                MatchId = match.MatchId,
                OpponentTeamId = match.OpponentTeamId,
                OpponentName = match.OpponentName,
                IsHome = match.IsHome,
                GoalsFor = match.GoalsFor,
                GoalsAgainst = match.GoalsAgainst,
                RoundNumber = match.RoundNumber,
                CompetitionName = match.CompetitionName,
                PhaseName = match.PhaseName,
                PlayedAt = match.PlayedAt
            })
            .ToList(),
        Squad = board.Squad
            .Select(row => new TacticsSquadRowDto
            {
                PlayerId = row.PlayerId,
                Name = row.Name,
                Age = row.Age,
                Position = row.Position.ToString(),
                Stars = row.Stars,
                Energy = row.Energy,
                IsAvailable = row.IsAvailable,
                Attributes = [.. row.Attributes]
            })
            .ToList(),
        Plan = board.Plan is null ? null : Map(board.Plan)
    };

    private static TacticsPlanDto Map(TacticsPlan plan) => new()
    {
        TacticCode = plan.TacticCode,
        StarterIds = [.. plan.StarterIds],
        BenchIds = [.. plan.BenchIds],
        UpdatedAt = plan.UpdatedAt
    };
}