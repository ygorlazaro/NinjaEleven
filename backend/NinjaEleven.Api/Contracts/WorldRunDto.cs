using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Contracts;

/// <summary>
/// A window of football the calendar says is due.
/// </summary>
public class DueRoundDto
{
    public required Guid MatchDayId { get; init; }
    public required int MatchDayNumber { get; init; }
    public required Guid RoundId { get; init; }
    public required int RoundNumber { get; init; }
    public required string Wave { get; init; }
    public required DateTimeOffset KickOffAt { get; init; }
}

/// <summary>
/// What one window of football cost, and what it left behind.
/// </summary>
public class RoundRunDto
{
    public required Guid RoundId { get; init; }
    public required string Claim { get; init; }
    public required int Played { get; init; }
    public required int AlreadyPlayed { get; init; }
    public required int Failed { get; init; }
    public required int PlayedElsewhere { get; init; }

    /// <summary>The fixtures that were started and are waiting for the manager to play them.</summary>
    public required int LeftForTheManager { get; init; }

    public required bool IsComplete { get; init; }
    public required double DurationSeconds { get; init; }
    public required IReadOnlyList<FixtureRunDto> Fixtures { get; init; }
}

/// <summary>What happened to one fixture while its window was being played.</summary>
public class FixtureRunDto
{
    public required Guid FixtureId { get; init; }
    public Guid? MatchId { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public required string Status { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// One step of the world walked by hand: which day, which window of it, and what was played.
/// </summary>
public class WorldAdvanceDto
{
    /// <summary>Nothing, Window or SeasonClosed.</summary>
    public required string Kind { get; init; }

    public Guid? SeasonId { get; init; }
    public string? SeasonName { get; init; }
    public int? MatchDayNumber { get; init; }
    public DateOnly? MatchDayDate { get; init; }
    public string? Wave { get; init; }
    public required IReadOnlyList<RoundRunDto> Rounds { get; init; }
    public bool SeasonClosed { get; init; }
    public Guid? SeasonOpened { get; init; }
}

public static class WorldRunMappings
{
    public static WorldAdvanceDto ToDto(this WorldAdvance advance) => new()
    {
        Kind = advance.Kind.ToString(),
        SeasonId = advance.SeasonId,
        SeasonName = advance.SeasonName,
        MatchDayNumber = advance.MatchDayNumber,
        MatchDayDate = advance.MatchDayDate,
        Wave = advance.Wave?.ToString(),
        Rounds = advance.Rounds.Select(run => run.ToDto()).ToList(),
        SeasonClosed = advance.SeasonClosed,
        SeasonOpened = advance.SeasonOpened
    };
    public static DueRoundDto ToDto(this DueRound round) => new()
    {
        MatchDayId = round.MatchDayId,
        MatchDayNumber = round.MatchDayNumber,
        RoundId = round.RoundId,
        RoundNumber = round.RoundNumber,
        Wave = round.Wave.ToString(),
        KickOffAt = round.KickOffAt
    };

    public static RoundRunDto ToDto(this RoundRun run) => new()
    {
        RoundId = run.RoundId,
        Claim = run.Claim.ToString(),
        Played = run.Played,
        AlreadyPlayed = run.AlreadyPlayed,
        Failed = run.Failed,
        PlayedElsewhere = run.PlayedElsewhere,
        LeftForTheManager = run.LeftForTheManager,
        IsComplete = run.IsComplete,
        DurationSeconds = run.Duration.TotalSeconds,
        Fixtures = run.Fixtures.Select(fixture => fixture.ToDto()).ToList()
    };

    public static FixtureRunDto ToDto(this FixtureRun run) => new()
    {
        FixtureId = run.FixtureId,
        MatchId = run.MatchId,
        HomeScore = run.HomeScore,
        AwayScore = run.AwayScore,
        Status = run.Status.ToString(),
        Error = run.Error
    };
}
