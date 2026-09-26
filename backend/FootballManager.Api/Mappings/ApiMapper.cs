using FootballManager.Api.Contracts;
using FootballManager.Application.Models;

namespace FootballManager.Api.Mappings;

public static class ApiMapper
{
    public static PlayerDto ToDto(this Domain.Players.Player player) => new()
    {
        Id = player.Id,
        Name = player.Name,
        BirthDate = player.BirthDate,
        Age = player.CalculateAge(),
        Position = player.Position,
        Speed = player.Speed,
        Accuracy = player.Accuracy,
        Dribbling = player.Dribbling,
        Heading = player.Heading,
        Strength = player.Strength,
        GoalkeeperPower = player.GoalkeeperPower,
        Reflexes = player.Reflexes
    };

    public static IReadOnlyList<PlayerDto> ToDtos(this IEnumerable<Domain.Players.Player> players) =>
        players.Select(player => player.ToDto()).ToList();

    public static PlayerSeasonStateDto ToDto(this Domain.Players.PlayerSeasonState state) => new()
    {
        Id = state.Id,
        PlayerId = state.PlayerId,
        SeasonId = state.SeasonId,
        TeamId = state.TeamId,
        Energy = state.Energy,
        Goals = state.Goals,
        YellowCards = state.YellowCards,
        RedCards = state.RedCards,
        SuspensionMatches = state.SuspensionMatches,
        Injury = state.Injury,
        IsAvailable = state.IsAvailable
    };

    public static SquadPlayerDto ToDto(this Application.Models.SquadPlayer squadPlayer) => new()
    {
        Id = squadPlayer.Player.Id,
        Name = squadPlayer.Player.Name,
        Age = squadPlayer.Player.CalculateAge(),
        Position = squadPlayer.Player.Position,
        Speed = squadPlayer.Player.Speed,
        Accuracy = squadPlayer.Player.Accuracy,
        Dribbling = squadPlayer.Player.Dribbling,
        Heading = squadPlayer.Player.Heading,
        Strength = squadPlayer.Player.Strength,
        GoalkeeperPower = squadPlayer.Player.GoalkeeperPower,
        Reflexes = squadPlayer.Player.Reflexes,
        Energy = squadPlayer.SeasonState.Energy,
        Goals = squadPlayer.SeasonState.Goals,
        YellowCards = squadPlayer.SeasonState.YellowCards,
        RedCards = squadPlayer.SeasonState.RedCards,
        SuspensionMatches = squadPlayer.SeasonState.SuspensionMatches,
        Injury = squadPlayer.SeasonState.Injury,
        InjuryMatchesRemaining = squadPlayer.SeasonState.InjuryMatchesRemaining,
        IsAvailable = squadPlayer.IsAvailable,
        TeamId = squadPlayer.SeasonState.TeamId,
        SeasonId = squadPlayer.SeasonState.SeasonId
    };

    public static TeamDto ToDto(this Domain.Teams.Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        ShortName = team.ShortName,
        PrimaryColor = team.PrimaryColor,
        SecondaryColor = team.SecondaryColor,
        Rating = team.Rating
    };

    public static IReadOnlyList<TeamDto> ToDtos(this IEnumerable<Domain.Teams.Team> teams) =>
        teams.Select(team => team.ToDto()).ToList();

    public static CompetitionDto ToDto(this Domain.Competitions.Competition competition) => new()
    {
        Id = competition.Id,
        Name = competition.Name,
        Type = competition.Type
    };

    public static IReadOnlyList<CompetitionDto> ToDtos(this IEnumerable<Domain.Competitions.Competition> competitions) =>
        competitions.Select(competition => competition.ToDto()).ToList();

    public static SeasonDto ToDto(this Domain.Seasons.Season season) => new()
    {
        Id = season.Id,
        Name = season.Name,
        StartDate = season.StartDate,
        EndDate = season.EndDate,
        Status = season.Status
    };

    public static IReadOnlyList<SeasonDto> ToDtos(this IEnumerable<Domain.Seasons.Season> seasons) =>
        seasons.Select(season => season.ToDto()).ToList();

    public static RoundDto ToDto(this Domain.Competitions.Round round) => new()
    {
        Id = round.Id,
        CompetitionSeasonId = round.CompetitionSeasonId,
        Number = round.Number
    };

    public static IReadOnlyList<RoundDto> ToDtos(this IEnumerable<Domain.Competitions.Round> rounds) =>
        rounds.Select(round => round.ToDto()).ToList();

    public static FixtureDto ToDto(this Application.Models.FixtureDetails details) => new()
    {
        Id = details.Fixture.Id,
        RoundId = details.Fixture.RoundId,
        HomeTeamId = details.Fixture.HomeTeamId,
        AwayTeamId = details.Fixture.AwayTeamId,
        Status = details.Fixture.Status,
        HomeTeam = details.HomeTeam?.ToDto(),
        AwayTeam = details.AwayTeam?.ToDto(),
        HomeGoals = details.Match?.HomeScore,
        AwayGoals = details.Match?.AwayScore,
        MatchId = details.Match?.Id
    };

    /// <summary>
    /// Mapping for a freshly created fixture, where the clubs and the result are not
    /// loaded yet. The identifiers are enough for the client to render the calendar.
    /// </summary>
    public static FixtureDto ToDto(this Domain.Matches.Fixture fixture) => new()
    {
        Id = fixture.Id,
        RoundId = fixture.RoundId,
        HomeTeamId = fixture.HomeTeamId,
        AwayTeamId = fixture.AwayTeamId,
        Status = fixture.Status
    };

    public static IReadOnlyList<FixtureDto> ToDtos(this IEnumerable<Application.Models.FixtureDetails> details) =>
        details.Select(detail => detail.ToDto()).ToList();

    public static MatchDto ToDto(this Application.Models.MatchSnapshot snapshot) => new()
    {
        Id = snapshot.Match.Id,
        FixtureId = snapshot.Match.FixtureId,
        HomeTeamId = snapshot.Match.HomeTeamId,
        AwayTeamId = snapshot.Match.AwayTeamId,
        Status = snapshot.Match.Status,
        Half = snapshot.Match.Half,
        CurrentMinute = snapshot.Match.CurrentMinute,
        HomeScore = snapshot.Match.HomeScore,
        AwayScore = snapshot.Match.AwayScore,
        Sequence = snapshot.Match.Sequence,
        Seed = snapshot.Match.Seed,
        HomeTeam = snapshot.HomeTeam?.ToDto(),
        AwayTeam = snapshot.AwayTeam?.ToDto(),
        Events = snapshot.Events.Select(matchEvent => matchEvent.ToDto()).ToList()
    };

    public static MatchDto ToDto(this Domain.Matches.Match match) => new()
    {
        Id = match.Id,
        FixtureId = match.FixtureId,
        HomeTeamId = match.HomeTeamId,
        AwayTeamId = match.AwayTeamId,
        Status = match.Status,
        Half = match.Half,
        CurrentMinute = match.CurrentMinute,
        HomeScore = match.HomeScore,
        AwayScore = match.AwayScore,
        Sequence = match.Sequence,
        Seed = match.Seed,
        Events = Array.Empty<MatchEventDto>()
    };

    public static IReadOnlyList<MatchDto> ToDtos(this IEnumerable<Domain.Matches.Match> matches) =>
        matches.Select(match => match.ToDto()).ToList();

    public static MatchEventDto ToDto(this Domain.Matches.MatchEvent matchEvent) => new()
    {
        Id = matchEvent.Id,
        MatchId = matchEvent.MatchId,
        Sequence = matchEvent.Sequence,
        Minute = matchEvent.Minute,
        Type = matchEvent.Type,
        TeamId = matchEvent.TeamId,
        PlayerId = matchEvent.PlayerId,
        SecondaryPlayerId = matchEvent.SecondaryPlayerId,
        HomeScore = matchEvent.HomeScore,
        AwayScore = matchEvent.AwayScore,
        Payload = matchEvent.Payload,
        Description = ReadPayload(matchEvent.Payload, "description"),
        Icon = ReadPayload(matchEvent.Payload, "icon")
    };

    /// <summary>
    /// The narration lives in the payload; this lifts a single field out of it so a
    /// reconnecting client can rebuild the feed without parsing JSON itself.
    /// </summary>
    private static string ReadPayload(string payload, string field)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty(field, out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }

    public static IReadOnlyList<MatchEventDto> ToDtos(this IEnumerable<Domain.Matches.MatchEvent> matchEvents) =>
        matchEvents.Select(matchEvent => matchEvent.ToDto()).ToList();

    public static StandingDto ToDto(this Application.Models.StandingRow row) => new()
    {
        TeamId = row.TeamId,
        Team = row.Team?.ToDto(),
        Points = row.Points,
        Played = row.Played,
        Wins = row.Wins,
        Draws = row.Draws,
        Losses = row.Losses,
        GoalsFor = row.GoalsFor,
        GoalsAgainst = row.GoalsAgainst,
        GoalDifference = row.GoalDifference,
        YellowCards = row.YellowCards,
        RedCards = row.RedCards
    };

    public static IReadOnlyList<StandingDto> ToDtos(this IEnumerable<Application.Models.StandingRow> rows) =>
        rows.Select(row => row.ToDto()).ToList();

    public static ScorerDto ToDto(this Application.Models.ScorerRow row) => new()
    {
        PlayerId = row.PlayerId,
        PlayerName = row.PlayerName,
        Age = row.Age,
        Goals = row.Goals,
        TeamId = row.TeamId,
        TeamName = row.TeamName
    };

    public static IReadOnlyList<ScorerDto> ToDtos(this IEnumerable<Application.Models.ScorerRow> rows) =>
        rows.Select(row => row.ToDto()).ToList();

    public static MatchCommandResultDto ToDto(this Application.Models.MatchCommandResult result) => new()
    {
        Accepted = result.Accepted,
        MatchId = result.MatchId,
        ErrorMessage = result.ErrorMessage,
        Events = result.Events.Select(engineEvent => engineEvent.ToDto()).ToList()
    };

    public static MatchEngineEventDto ToDto(this Domain.Matches.MatchEngineEvent engineEvent) => new()
    {
        Sequence = engineEvent.Sequence,
        Minute = engineEvent.Minute,
        Type = engineEvent.Type,
        TeamId = engineEvent.TeamId,
        PlayerId = engineEvent.PlayerId,
        HomeScore = engineEvent.HomeScore,
        AwayScore = engineEvent.AwayScore,
        Icon = engineEvent.Icon,
        Description = engineEvent.Description
    };

    public static IReadOnlyList<MatchEngineEventDto> ToDtos(
        this IEnumerable<Domain.Matches.MatchEngineEvent> events) =>
        events.Select(engineEvent => engineEvent.ToDto()).ToList();

    public static MatchPlayerDto ToDto(this Domain.Matches.MatchPlayerSnapshot player) => new()
    {
        PlayerId = player.PlayerId,
        Name = player.Name,
        Age = player.Age,
        Position = player.Position,
        Speed = player.Speed,
        Accuracy = player.Accuracy,
        Dribbling = player.Dribbling,
        Heading = player.Heading,
        Strength = player.Strength,
        GoalkeeperPower = player.GoalkeeperPower,
        Reflexes = player.Reflexes,
        Energy = player.Energy,
        MatchYellowCards = player.MatchYellowCards,
        RedCard = player.RedCard,
        EmergencyGK = player.EmergencyGK,
        InjuredOff = player.InjuredOff,
        SubbedIn = player.SubbedIn,
        Goals = player.Goals
    };

    public static MatchLineupDto ToDto(this Application.Models.MatchLineup lineup) => new()
    {
        MatchId = lineup.MatchId,
        UserTeamIndex = lineup.UserTeamIndex,
        HomeTeam = lineup.HomeTeam.ToDto(),
        AwayTeam = lineup.AwayTeam.ToDto(),
        HomeLineup = lineup.HomeLineup.Select(player => player.ToDto()).ToList(),
        AwayLineup = lineup.AwayLineup.Select(player => player.ToDto()).ToList(),
        HomeBench = lineup.HomeBench.Select(player => player.ToDto()).ToList(),
        AwayBench = lineup.AwayBench.Select(player => player.ToDto()).ToList()
    };

    public static PenaltyTakerOptionsDto ToDto(this Application.Models.PenaltyTakerOptions options) => new()
    {
        AwaitingSelection = options.AwaitingSelection,
        Candidates = options.Candidates.Select(player => player.ToDto()).ToArray()
    };

    public static MatchStateDto ToDto(this Application.Models.MatchStateView state) => new()
    {
        MatchId = state.MatchId,
        HomeScore = state.HomeScore,
        AwayScore = state.AwayScore,
        Minute = state.Minute,
        Second = state.Second,
        CurrentHalf = (Domain.Enums.MatchHalf)state.Half,
        Sequence = state.Sequence,
        StoppageTimeMinutes = state.StoppageTimeMinutes,
        Status = Enum.TryParse<Domain.Enums.MatchStatus>(state.Status, out var status)
            ? status
            : Domain.Enums.MatchStatus.Scheduled,
        IsPaused = state.IsPaused,
        IsHalfTime = state.IsHalfTime,
        IsFinished = state.IsFinished,
        Speed = state.Speed,
        Stats =
        [
            new TeamMatchStatsDto
            {
                Shots = state.HomeShots,
                ShotsOnTarget = state.HomeShotsOnTarget,
                Corners = state.HomeCorners,
                Cards = state.HomeCards,
                Fouls = state.HomeFouls,
                Possession = state.HomePossession
            },
            new TeamMatchStatsDto
            {
                Shots = state.AwayShots,
                ShotsOnTarget = state.AwayShotsOnTarget,
                Corners = state.AwayCorners,
                Cards = state.AwayCards,
                Fouls = state.AwayFouls,
                Possession = state.AwayPossession
            }
        ],
        Possession = new MatchPossessionDto
        {
            Team = state.HomePossession >= state.AwayPossession ? 0 : 1
        },
        PenaltyAwaitingSelection = state.PenaltyAwaitingSelection,
        Penalty = state.Penalty.ToDto(),
        UserTeamId = state.UserTeamId,
        SubstitutionsUsedHome = state.SubstitutionsUsedHome,
        SubstitutionsUsedAway = state.SubstitutionsUsedAway
    };

    public static MatchResultDto ToDto(this Application.Models.MatchResultView result) => new()
    {
        HomeScore = result.HomeScore,
        AwayScore = result.AwayScore,
        PlayerStats = Array.Empty<PlayerMatchStatsDto>(),
        HomeShots = result.HomeShots,
        AwayShots = result.AwayShots,
        HomeShotsOnTarget = result.HomeShotsOnTarget,
        AwayShotsOnTarget = result.AwayShotsOnTarget,
        HomeCorners = result.HomeCorners,
        AwayCorners = result.AwayCorners,
        HomeCards = result.HomeCards,
        AwayCards = result.AwayCards,
        HomeFouls = result.HomeFouls,
        AwayFouls = result.AwayFouls,
        HomePossession = result.HomePossession,
        AwayPossession = result.AwayPossession,
        FormationHome = result.FormationHome,
        FormationAway = result.FormationAway,
        SubstitutionsHome = result.SubstitutionsHome,
        SubstitutionsAway = result.SubstitutionsAway
    };
}

public static class MatchScoreMapper
{
    public static MatchScoreDto ToDto(this MatchScoreRow row) => new()
    {
        RoundId = row.RoundId,
        MatchId = row.MatchId,
        FixtureId = row.FixtureId,
        HomeTeamId = row.HomeTeamId,
        HomeTeamName = row.HomeTeamName,
        HomeShortName = row.HomeShortName,
        AwayTeamId = row.AwayTeamId,
        AwayTeamName = row.AwayTeamName,
        AwayShortName = row.AwayShortName,
        HomeGoals = row.HomeGoals,
        AwayGoals = row.AwayGoals,
        Minute = row.Minute,
        Half = row.Half,
        Status = row.Status,
        IsFinished = row.IsFinished
    };
}
