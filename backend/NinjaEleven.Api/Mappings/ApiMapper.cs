using NinjaEleven.Api.Contracts;
using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Api.Mappings;

public static class ApiMapper
{
public static PlayerDto ToDto(this Domain.Players.Player player) => new()
{
    Id = player.Id,
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
    Stamina = player.Stamina,
    Potential = player.Potential,
    Stars = Domain.Players.PlayerRating.CalculateStars(player)
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
    Age = squadPlayer.Player.Age,
    Position = squadPlayer.Player.Position,
    Speed = squadPlayer.Player.Speed,
    Accuracy = squadPlayer.Player.Accuracy,
    Dribbling = squadPlayer.Player.Dribbling,
    Heading = squadPlayer.Player.Heading,
    Strength = squadPlayer.Player.Strength,
    GoalkeeperPower = squadPlayer.Player.GoalkeeperPower,
    Reflexes = squadPlayer.Player.Reflexes,
    Stamina = squadPlayer.Player.Stamina,
    Potential = squadPlayer.Player.Potential,
    Stars = Domain.Players.PlayerRating.CalculateStars(squadPlayer.Player),
    Energy = squadPlayer.SeasonState.Energy,
    Goals = squadPlayer.SeasonState.Goals,
    Saves = squadPlayer.SeasonState.Saves,
    YellowCards = squadPlayer.SeasonState.YellowCards,
    RedCards = squadPlayer.SeasonState.RedCards,
    SuspensionMatches = squadPlayer.SeasonState.SuspensionMatches,
    Injury = squadPlayer.SeasonState.Injury,
    InjuryMatchesRemaining = squadPlayer.SeasonState.InjuryMatchesRemaining,
    Injuries = squadPlayer.SeasonState.Injuries,
    ContractSeasons = squadPlayer.ContractSeasons,
    SeasonsLeft = squadPlayer.SeasonsLeft,
    IsInLastSeason = squadPlayer.IsInLastSeason,
    MarketValue = squadPlayer.MarketValue,
    AskingPrice = squadPlayer.AskingPrice,
    ReleaseCost = squadPlayer.ReleaseCost,
    Salary = squadPlayer.Salary,
    WageOnRenewal = squadPlayer.WageOnRenewal,
    ShirtNumber = squadPlayer.ShirtNumber,
    IsAvailable = squadPlayer.IsAvailable,
    Retiring = squadPlayer.Retiring,
    TeamId = squadPlayer.SeasonState.TeamId,
    SeasonId = squadPlayer.SeasonState.SeasonId
};

    public static TeamDto ToDto(this Domain.Teams.Team team, double stars = 0) => new()
    {
        Id = team.Id,
        Name = team.Name,
        ShortName = team.ShortName,
        PrimaryColor = team.PrimaryColor,
        SecondaryColor = team.SecondaryColor,
        Rating = (int)Math.Round(stars * 20), // 0-5 stars -> 0-100 scale
        Stars = stars,
        Stadium = team.Stadium != null ? new StadiumDto
        {
            Id = team.Stadium.Id,
            Name = team.Stadium.Name,
            Capacity = team.Stadium.Capacity,
            TicketPrice = team.Stadium.TicketPrice
        } : null,
        ControlledBy = team.Managers.Any()
            ? (bool?) team.Managers.Any(m => m.UserId.HasValue)
            : null,
        // The badge and the two shirts travel with the club rather than through an endpoint of
        // their own. A crest is who a club is, not a question a screen has to remember to ask
        // in a second call before it may draw a table cell: an answer that arrives late is an
        // answer that arrives after the cell has already been drawn blank.
        Crest = team.Crest is { } crest ? crest.ToDto() : null,
        HomeKit = team.HomeKit is { } homeKit ? homeKit.ToDto() : null,
        AwayKit = team.AwayKit is { } awayKit ? awayKit.ToDto() : null
    };

    /// <summary>
    /// A club's badge as the screens draw it. Null in, null out: a club with no crest is drawn
    /// from its initials rather than from an empty shape.
    /// </summary>
    public static CrestDto? ToDto(this Domain.Teams.CrestDesign? crest) => crest is null ? null : new CrestDto
    {
        Shape = crest.Shape,
        PrimaryColor = crest.PrimaryColor,
        SecondaryColor = crest.SecondaryColor,
        Text = crest.Text is { } text ? new CrestTextDto
        {
            Content = text.Content,
            Color = text.Color,
            VerticalPosition = text.VerticalPosition
        } : null,
        Emblem = crest.Emblem is { } emblem ? new CrestEmblemDto
        {
            Kind = emblem.Kind,
            Color = emblem.Color,
            VerticalPosition = emblem.VerticalPosition
        } : null
    };

    public static KitDto ToDto(this Domain.Teams.KitDesign kit) => new()
    {
        PrimaryColor = kit.PrimaryColor,
        SecondaryColor = kit.SecondaryColor,
        Pattern = kit.Pattern,
        TrimColor = kit.TrimColor
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

    public static CompetitionEditionDto ToDto(this Application.Models.CompetitionSeasonView view) => new()
    {
        Id = view.Id,
        CompetitionId = view.CompetitionId,
        SeasonId = view.SeasonId,
        DivisionId = view.DivisionId,
        Tier = view.Tier,
        CompetitionName = view.CompetitionName,
        Type = view.Type,
        Name = view.Name,
        IsDivision = view.IsDivision
    };

    public static IReadOnlyList<CompetitionEditionDto> ToDtos(
        this IEnumerable<Application.Models.CompetitionSeasonView> views) =>
        views.Select(view => view.ToDto()).ToList();

    public static SeasonDto ToDto(this Domain.Seasons.Season season) => new()
    {
        Id = season.Id,
        Number = season.Number,
        Name = season.Name,
        StartDate = season.StartDate,
        EndDate = season.EndDate,
        Status = season.Status
    };

    public static IReadOnlyList<SeasonDto> ToDtos(this IEnumerable<Domain.Seasons.Season> seasons) =>
        seasons.Select(season => season.ToDto()).ToList();

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
        Attendance = snapshot.Match.Attendance,
        GateRevenue = snapshot.Match.Gate.GrossRevenue,
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
        Attendance = match.Attendance,
        GateRevenue = match.Gate.GrossRevenue,
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
        Icon = ReadPayload(matchEvent.Payload, "icon"),
        FromPenalty = ReadPayloadBool(matchEvent.Payload, "fromPenalty"),
        PlayerName = matchEvent.PlayerName
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

    /// <summary>
    /// The same reading for a flag. An event written before the field existed has no such
    /// property, and an old goal is not a penalty — the answer is a plain no rather than a
    /// failure, because a row nobody rewrote is a row that never knew.
    /// </summary>
    private static bool ReadPayloadBool(string payload, string field)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty(field, out var value)
                && value.ValueKind is System.Text.Json.JsonValueKind.True;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static IReadOnlyList<MatchEventDto> ToDtos(this IEnumerable<Domain.Matches.MatchEvent> matchEvents) =>
        matchEvents.Select(matchEvent => matchEvent.ToDto()).ToList();

    /// <summary>
    /// A persisted event in the shape the live feed uses.
    ///
    /// The engine's own event and the row it was written to carry the same facts, and a
    /// client should not be able to tell which of the two it was sent: a manager who opened
    /// his match at minute sixty is sent the state and then the events of a match the
    /// scheduler is playing, and those events arrive as themselves rather than as a different
    /// kind of message that happens to mean the same thing.
    /// </summary>
    public static MatchEngineEventDto ToEngineDto(this MatchEventDto matchEvent) => new()
    {
        Sequence = matchEvent.Sequence,
        Minute = matchEvent.Minute,
        Type = matchEvent.Type,
        TeamId = matchEvent.TeamId,
        PlayerId = matchEvent.PlayerId,
        HomeScore = matchEvent.HomeScore,
        AwayScore = matchEvent.AwayScore,
        Icon = matchEvent.Icon,
        Description = matchEvent.Description
    };

    public static IReadOnlyList<MatchEngineEventDto> ToEngineDtos(this IEnumerable<MatchEventDto> matchEvents) =>
        matchEvents.Select(matchEvent => matchEvent.ToEngineDto()).ToList();

    public static RoundDto ToDto(this Domain.Competitions.Round round) => new()
    {
        Id = round.Id,
        CompetitionSeasonId = round.CompetitionSeasonId,
        Number = round.Number,
        MatchDayId = round.MatchDayId,
        Window = round.Window,
        CompletedAt = round.CompletedAt
    };

    public static IReadOnlyList<RoundDto> ToDtos(this IEnumerable<Domain.Competitions.Round> rounds) =>
        rounds.Select(round => round.ToDto()).ToList();

    public static MatchDayDto ToDto(this Domain.Competitions.MatchDay matchDay) => new()
    {
        Id = matchDay.Id,
        SeasonId = matchDay.SeasonId,
        Number = matchDay.Number,
        Date = matchDay.Date
    };

    public static SeasonCalendarDto ToDto(this Application.Models.SeasonCalendar calendar) => new()
    {
        SeasonId = calendar.SeasonId,
        SeasonName = calendar.SeasonName,
        MatchDayCount = calendar.MatchDayCount,
        MatchDays = calendar.MatchDays.Select(matchDay => matchDay.ToDto()).ToList(),
        Windows = calendar.Windows.ToDtos()
    };

    public static StandingDto ToDto(this Application.Models.StandingRow row) => new()
    {
        TeamId = row.TeamId,
        // The club inside a table carries the strength that table seeded it with. A club drawn
        // with no stars in a table that ranked it by them is a club that reads as worthless.
        Team = row.Team?.ToDto(row.Stars),
        Position = row.Position,
        Points = row.Points,
        Played = row.Played,
        Wins = row.Wins,
        Draws = row.Draws,
        Losses = row.Losses,
        GoalsFor = row.GoalsFor,
        GoalsAgainst = row.GoalsAgainst,
        GoalDifference = row.GoalDifference,
        YellowCards = row.YellowCards,
        RedCards = row.RedCards,
        Stars = row.Stars,
        Zone = row.Zone,
        Form = row.Form
    };

    public static IReadOnlyList<StandingDto> ToDtos(this IEnumerable<Application.Models.StandingRow> rows) =>
        rows.Select(row => row.ToDto()).ToList();

    public static ClubStandingDto ToDto(this Application.Models.ClubStanding standing) => new()
    {
        TeamId = standing.TeamId,
        SeasonId = standing.SeasonId,
        CompetitionSeasonId = standing.CompetitionSeasonId,
        DivisionName = standing.DivisionName,
        Tier = standing.Tier,
        SquadStars = standing.SquadStars,
        ClubsInDivision = standing.ClubsInDivision,
        Row = standing.Row?.ToDto()
    };

    public static CompetitionStandingsDto ToDto(this Application.Models.CompetitionStandings standings) => new()
    {
        CompetitionSeasonId = standings.CompetitionSeasonId,
        SeasonId = standings.SeasonId,
        DivisionId = standings.DivisionId,
        Tier = standings.Tier,
        CompetitionName = standings.CompetitionName,
        Official = standings.Official.ToDtos(),
        Projected = standings.Projected.ToDtos(),
        HasLiveMatches = standings.HasLiveMatches
    };

    public static ScorerDto ToDto(this Application.Models.ScorerRow row) => new()
    {
        PlayerId = row.PlayerId,
        PlayerName = row.PlayerName,
        Age = row.Age,
        Goals = row.Goals,
        TeamId = row.TeamId,
        TeamName = row.TeamName,
        TeamPrimaryColor = row.TeamPrimaryColor,
        TeamSecondaryColor = row.TeamSecondaryColor,
        Position = row.Position,
        TiedWith = row.TiedWith,
        Appearances = row.Appearances,
        YellowCards = row.YellowCards,
        RedCards = row.RedCards,
        CardPoints = row.CardPoints
    };

    public static TopScorerPrizeListDto ToDto(this Application.Models.TopScorerPrizeList list) => new()
    {
        CompetitionSeasonId = list.CompetitionSeasonId,
        SeasonId = list.SeasonId,
        CompetitionName = list.CompetitionName,
        Tier = list.Tier,
        BaseAmount = list.BaseAmount,
        Rates = list.Rates.Select(rate => new TopScorerRateDto
        {
            Place = rate.Place,
            Rate = rate.Rate
        }).ToList(),
        Winners = list.Winners.Select(winner => winner.ToDto()).ToList()
    };

    public static TopScorerPrizeDto ToDto(this Application.Models.TopScorerPrizeRow row) => new()
    {
        PlayerId = row.PlayerId,
        PlayerName = row.PlayerName,
        Age = row.Age,
        TeamId = row.TeamId,
        TeamName = row.TeamName,
        TeamPrimaryColor = row.TeamPrimaryColor,
        TeamSecondaryColor = row.TeamSecondaryColor,
        Position = row.Position,
        PrizeSlot = row.PrizeSlot,
        TiedWith = row.TiedWith,
        Goals = row.Goals,
        Appearances = row.Appearances,
        CardPoints = row.CardPoints,
        Rate = row.Rate,
        Amount = row.Amount
    };

    public static ClubScorerDto ToDto(this Application.Models.ClubScorerRow row) => new()
    {
        PlayerId = row.PlayerId,
        PlayerName = row.PlayerName,
        Age = row.Age,
        Position = row.Position,
        Goals = row.Goals,
        OwnGoals = row.OwnGoals,
        Started = row.Started,
        CameOn = row.CameOn,
        Appearances = row.Appearances,
        YellowCards = row.YellowCards,
        RedCards = row.RedCards,
        CardPoints = row.CardPoints,
        TiedWith = row.TiedWith,
        GoalsPerAppearance = row.GoalsPerAppearance,
        IsStillAtClub = row.IsStillAtClub
    };

    public static IReadOnlyList<ClubScorerDto> ToDtos(this IEnumerable<Application.Models.ClubScorerRow> rows) =>
        rows.Select(row => row.ToDto()).ToList();

    public static IReadOnlyList<ScorerDto> ToDtos(this IEnumerable<Application.Models.ScorerRow> rows) =>
        rows.Select(row => row.ToDto()).ToList();

    public static CupBracketDto ToDto(this Application.Models.CupBracketView bracket) => new()
    {
        CompetitionSeasonId = bracket.CompetitionSeasonId,
        SeasonId = bracket.SeasonId,
        CompetitionName = bracket.CompetitionName,
        Rounds = bracket.Rounds.Select(round => round.ToDto()).ToList(),
        ChampionTeamId = bracket.ChampionTeamId,
        ChampionTeamName = bracket.ChampionTeamName,
        RunnerUpTeamName = bracket.RunnerUpTeamName
    };

    public static CupBracketRoundDto ToDto(this Application.Models.CupBracketRound round) => new()
    {
        RoundNumber = round.RoundNumber,
        Name = round.Name,
        Ties = round.Ties.Select(tie => tie.ToDto()).ToList()
    };

    public static CupBracketTieDto ToDto(this Application.Models.CupBracketTie tie) => new()
    {
        TieId = tie.TieId,
        RoundNumber = tie.RoundNumber,
        Clubs = tie.Clubs.Select(club => club.ToDto()).ToList(),
        FirstLegScore = tie.FirstLegScore,
        SecondLegScore = tie.SecondLegScore,
        FirstLegMatchId = tie.FirstLegMatchId,
        SecondLegMatchId = tie.SecondLegMatchId,
        FirstLegLive = tie.FirstLegLive,
        SecondLegLive = tie.SecondLegLive
    };

    public static CupBracketClubDto ToDto(this Application.Models.CupBracketClub club) => new()
    {
        TeamId = club.TeamId,
        Name = club.Name,
        PrimaryColor = club.PrimaryColor,
        SecondaryColor = club.SecondaryColor,
        FirstLegGoals = club.FirstLegGoals,
        FirstLegConceded = club.FirstLegConceded,
        SecondLegGoals = club.SecondLegGoals,
        SecondLegConceded = club.SecondLegConceded,
        AggregateGoals = club.AggregateGoals,
        AggregateConceded = club.AggregateConceded,
        PenaltyGoals = club.PenaltyGoals,
        IsWinner = club.IsWinner,
        IsLoser = club.IsLoser
    };

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
        Description = engineEvent.Description,
        FromPenalty = engineEvent.FromPenalty,
        PlayerName = engineEvent.PlayerName
    };

    public static IReadOnlyList<MatchEngineEventDto> ToDtos(
        this IEnumerable<Domain.Matches.MatchEngineEvent> events) =>
        events.Select(engineEvent => engineEvent.ToDto()).ToList();

/// <param name="minute">
/// How far the match has got. A rating is a reading of the evening so far and cannot be
/// worked out without it, so a match that has not kicked off has no ratings at all rather
/// than a full eleven of them.
/// </param>
public static MatchPlayerDto ToDto(
    this Domain.Matches.MatchPlayerSnapshot player,
    int minute,
    double? penaltyChance = null)
{
    var minutesPlayed = player.MinutesPlayed(minute);
    var rating = Domain.Matches.MatchRating.Of(player, minutesPlayed, minute);

    return new MatchPlayerDto
    {
    PlayerId = player.PlayerId,
    Name = player.Name,
    Age = player.Age,
    Position = player.Position,
    ShirtNumber = player.ShirtNumber,
    Speed = player.Speed,
    Accuracy = player.Accuracy,
    Dribbling = player.Dribbling,
    Heading = player.Heading,
    Strength = player.Strength,
    GoalkeeperPower = player.GoalkeeperPower,
    Reflexes = player.Reflexes,
    Stamina = player.Stamina,
    Energy = player.Energy,
    MatchYellowCards = player.MatchYellowCards,
    RedCard = player.RedCard,
    EmergencyGK = player.EmergencyGK,
    InjuredOff = player.InjuredOff,
    SubbedIn = player.SubbedIn,
    SubbedOff = player.SubbedOff,
    Goals = player.Goals,
    MatchGoals = player.MatchGoals,
    MatchOwnGoals = player.MatchOwnGoals,
    MatchSaves = player.MatchSaves,
    PenaltyChance = penaltyChance,
    Stars = Domain.Players.PlayerRating.CalculateStars(player),
    MinutesPlayed = minutesPlayed,
    Rating = rating,
    RatingBand = Domain.Matches.MatchRating.BandOf(rating)
    };
}

public static MatchLineupDto ToDto(this Application.Models.MatchLineup lineup)
{
    var allHomePlayers = lineup.HomeLineup.Concat(lineup.HomeBench).ToList();
    var allAwayPlayers = lineup.AwayLineup.Concat(lineup.AwayBench).ToList();
    var homeStars = Domain.Players.PlayerRating.CalculateTeamStarsFromSnapshots(allHomePlayers);
    var awayStars = Domain.Players.PlayerRating.CalculateTeamStarsFromSnapshots(allAwayPlayers);

    var homeTeam = lineup.HomeTeam;
    var awayTeam = lineup.AwayTeam;

    return new MatchLineupDto
    {
        MatchId = lineup.MatchId,
        UserTeamIndex = lineup.UserTeamIndex,

        // The two clubs are mapped the way every other club is mapped, so the badge and the
        // two shirts travel with them. Copying six fields by hand is what left the match
        // screen drawing a plain solid shirt in the club's colours while the club had a
        // striped one drawn in its own editor — the eleven were wearing a kit nobody chose.
        HomeTeam = homeTeam.ToDto(homeStars),
        AwayTeam = awayTeam.ToDto(awayStars),

        HomeLineup = lineup.HomeLineup.Select(player => player.ToDto(lineup.Minute)).ToList(),
        AwayLineup = lineup.AwayLineup.Select(player => player.ToDto(lineup.Minute)).ToList(),
        HomeBench = lineup.HomeBench.Select(player => player.ToDto(lineup.Minute)).ToList(),
        AwayBench = lineup.AwayBench.Select(player => player.ToDto(lineup.Minute)).ToList(),
        HomeKitSide = lineup.HomeKitSide,
        AwayKitSide = lineup.AwayKitSide
    };
}

public static PenaltyTakerOptionsDto ToDto(this Application.Models.PenaltyTakerOptions options) => new()
{
    AwaitingSelection = options.AwaitingSelection,
    Candidates = options.Candidates
        .Select(player => player.ToDto(minute: 0, Domain.Matches.MatchEngine.PenaltyConversion(
            player,
            options.DefendingGoalkeeper)))
        .ToArray()
};

public static ShootoutDto? ToDto(this Application.Models.ShootoutView? shootout) => shootout is null
    ? null
    : new ShootoutDto
    {
        HomeTeamId = shootout.HomeTeamId,
        AwayTeamId = shootout.AwayTeamId,
        HomeTakesFirst = shootout.HomeTakesFirst,
        NextTeamId = shootout.NextTeamId,
        NextTakerId = shootout.NextTakerId,
        HomeGoals = shootout.HomeGoals,
        AwayGoals = shootout.AwayGoals,
        HomeKicksTaken = shootout.HomeKicksTaken,
        AwayKicksTaken = shootout.AwayKicksTaken,
        IsSuddenDeath = shootout.IsSuddenDeath,
        IsComplete = shootout.IsComplete,
        WinnerTeamId = shootout.WinnerTeamId,
        AwaitingOrder = shootout.AwaitingOrder,
        Candidates = shootout.Candidates
            .Select(player => player.ToDto(minute: 0, Domain.Matches.MatchEngine.PenaltyConversion(
                player,
                shootout.DefendingGoalkeeper)))
            .ToArray(),
        HomeTakers = shootout.HomeTakers,
        AwayTakers = shootout.AwayTakers,
        Kicks = shootout.Kicks
            .Select(kick => new ShootoutKickDto
            {
                TeamId = kick.TeamId,
                TakerId = kick.TakerId,
                Scored = kick.Scored
            })
            .ToArray()
    };

public static MatchContextDto ToDto(this Application.Models.MatchContextView context) => new()
{
    SeasonName = context.SeasonName,
    MatchDayNumber = context.MatchDayNumber,
    CompetitionName = context.CompetitionName,
    CompetitionType = context.CompetitionType,
    EditionName = context.EditionName,
    PhaseName = context.PhaseName,
    LegLabel = context.LegLabel,
    StadiumName = context.StadiumName,
    StadiumCapacity = context.StadiumCapacity,
    FirstLeg = context.FirstLeg is null
        ? null
        : new CupLegResultDto
        {
            HomeTeamId = context.FirstLeg.HomeTeamId,
            HomeTeamName = context.FirstLeg.HomeTeamName,
            HomeGoals = context.FirstLeg.HomeGoals,
            AwayTeamId = context.FirstLeg.AwayTeamId,
            AwayTeamName = context.FirstLeg.AwayTeamName,
            AwayGoals = context.FirstLeg.AwayGoals
        }
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
            Team = state.PossessionTeam,
            PlayerId = state.PossessionPlayerId
        },
        HomePossessionPercent = state.HomePossession,
        AwayPossessionPercent = state.AwayPossession,
        PenaltyAwaitingSelection = state.PenaltyAwaitingSelection,
        PenaltyEndsAt = state.PenaltyEndsAt,
        HalfTimeEndsAt = state.HalfTimeEndsAt,
        Penalty = state.Penalty.ToDto(),
        Injury = new MatchInjuryDto
        {
            AwaitingSubstitution = state.Injury.AwaitingSubstitution,
            PlayerId = state.Injury.PlayerId,
            PlayerName = state.Injury.PlayerName,
            Team = state.Injury.Team,
            Severity = state.Injury.Severity
        },
        UserTeamId = state.UserTeamId,
        LiveRatings = state.LiveRatings
            .Select(rating => new LiveRatingDto
            {
                PlayerId = rating.PlayerId,
                Rating = rating.Rating,
                RatingBand = rating.RatingBand,
                MinutesPlayed = rating.MinutesPlayed
            })
            .ToArray(),
        SubstitutionsUsedHome = state.SubstitutionsUsedHome,
        SubstitutionsUsedAway = state.SubstitutionsUsedAway,
        FormationHome = state.FormationHome,
        FormationAway = state.FormationAway,
        Attendance = state.Attendance,
        GateRevenue = state.GateRevenue,
        Shootout = state.Shootout.ToDto()
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
    public static LiveMatchDto ToDto(this LiveMatchSummary summary) => new()
    {
        MatchId = summary.MatchId,
        RoundId = summary.RoundId,
        HomeTeamId = summary.HomeTeamId,
        HomeTeamName = summary.HomeTeamName,
        HomeShortName = summary.HomeShortName,
        HomePrimaryColor = summary.HomePrimaryColor,
        HomeSecondaryColor = summary.HomeSecondaryColor,
        AwayTeamId = summary.AwayTeamId,
        AwayTeamName = summary.AwayTeamName,
        AwayShortName = summary.AwayShortName,
        AwayPrimaryColor = summary.AwayPrimaryColor,
        AwaySecondaryColor = summary.AwaySecondaryColor,
        HomeGoals = summary.HomeGoals,
        AwayGoals = summary.AwayGoals,
        Minute = summary.Minute,
        IsHome = summary.IsHome,
        AtHalfTime = summary.AtHalfTime
    };

    public static MatchScoreDto ToDto(this MatchScoreRow row) => new()
    {
        RoundId = row.RoundId,
        MatchId = row.MatchId,
        FixtureId = row.FixtureId,
        HomeTeamId = row.HomeTeamId,
        HomeTeamName = row.HomeTeamName,
        HomeShortName = row.HomeShortName,
        HomePrimaryColor = row.HomePrimaryColor,
        HomeSecondaryColor = row.HomeSecondaryColor,
        AwayTeamId = row.AwayTeamId,
        AwayTeamName = row.AwayTeamName,
        AwayShortName = row.AwayShortName,
        AwayPrimaryColor = row.AwayPrimaryColor,
        AwaySecondaryColor = row.AwaySecondaryColor,
        HomeGoals = row.HomeGoals,
        AwayGoals = row.AwayGoals,
        Minute = row.Minute,
        Half = row.Half,
        Status = row.Status,
        IsFinished = row.IsFinished,
        HomeOwnGoals = row.HomeOwnGoals,
        AwayOwnGoals = row.AwayOwnGoals,
        HomeYellowCards = row.HomeYellowCards,
        AwayYellowCards = row.AwayYellowCards,
        HomeRedCards = row.HomeRedCards,
        AwayRedCards = row.AwayRedCards,
        HomeInjuries = row.HomeInjuries,
        AwayInjuries = row.AwayInjuries
    };
}

/// <summary>
/// The round report crosses over as it stands: the summary is prose the match already
/// wrote, and reformatting it on the way out would be the second telling.
/// </summary>
public static class MatchdayReportMapping
{
    public static MatchdayReportDto ToDto(this Application.Models.MatchdayReport report) => new()
    {
        RoundId = report.RoundId,
        RoundNumber = report.RoundNumber,
        Entries = report.Entries.Select(entry => new MatchdayReportEntryDto
        {
            FixtureId = entry.FixtureId,
            MatchId = entry.MatchId,
            HomeTeamId = entry.HomeTeamId,
            HomeTeamName = entry.HomeTeamName,
            HomeShortName = entry.HomeShortName,
            AwayTeamId = entry.AwayTeamId,
            AwayTeamName = entry.AwayTeamName,
            AwayShortName = entry.AwayShortName,
            HomeGoals = entry.HomeGoals,
            AwayGoals = entry.AwayGoals,
            Summary = entry.Summary
        }).ToList()
    };
}

/// <summary>
/// The player profile crosses over with its two totals columns intact. The appearance
/// pair is not merged into a single number on the way out: the screen is what decides to
/// print "14 (3)", and a DTO that had already thrown the "3" away would take that choice
/// away from it.
/// </summary>
public static class PlayerProfileMapping
{
    public static PlayerCareerLineDto ToDto(this Application.Models.PlayerCareerLine line) => new()
    {
        Appearances = line.Appearances,
        Started = line.Started,
        CameOn = line.CameOn,
        BenchUnused = line.BenchUnused,
        Goals = line.Goals,
        OwnGoals = line.OwnGoals,
        Saves = line.Saves,
        Assists = line.Assists,
        YellowCards = line.YellowCards,
        RedCards = line.RedCards,
        Injuries = line.Injuries,
        MatchesMissed = line.MatchesMissed,
        AverageRating = line.AverageRating,
        RatedMatches = line.RatedMatches
    };

public static PlayerProfileDto ToDto(this Application.Models.PlayerProfile profile) => new()
{
    PlayerId = profile.PlayerId,
    Name = profile.Name,
    Position = profile.Position,
    Age = profile.Age,
    Speed = profile.Speed,
    Accuracy = profile.Accuracy,
    Dribbling = profile.Dribbling,
    Heading = profile.Heading,
    Strength = profile.Strength,
    GoalkeeperPower = profile.GoalkeeperPower,
    Reflexes = profile.Reflexes,
    Stamina = profile.Stamina,
    Potential = profile.Potential,
    Stars = profile.Position == "GK"
        ? Domain.Players.PlayerRating.CalculateGoalkeeperStars(profile.Speed, profile.Accuracy, profile.GoalkeeperPower, profile.Reflexes, profile.Strength)
        : Domain.Players.PlayerRating.CalculateOutfieldStars(profile.Speed, profile.Accuracy, profile.Dribbling, profile.Heading, profile.Strength),
    SpeedStars = Domain.Players.PlayerRating.AttributeToStars(profile.Speed),
    AccuracyStars = Domain.Players.PlayerRating.AttributeToStars(profile.Accuracy),
    DribblingStars = Domain.Players.PlayerRating.AttributeToStars(profile.Dribbling),
    HeadingStars = Domain.Players.PlayerRating.AttributeToStars(profile.Heading),
    StrengthStars = Domain.Players.PlayerRating.AttributeToStars(profile.Strength),
    GoalkeeperPowerStars = Domain.Players.PlayerRating.AttributeToStars(profile.GoalkeeperPower),
    ReflexesStars = Domain.Players.PlayerRating.AttributeToStars(profile.Reflexes),
    Face = profile.Face,
    SeasonId = profile.SeasonId,
    TeamId = profile.TeamId,
    TeamName = profile.TeamName,
    TeamPrimaryColor = profile.TeamPrimaryColor,
    TeamSecondaryColor = profile.TeamSecondaryColor,
    Energy = profile.Energy,
    IsAvailable = profile.IsAvailable,
    Injury = profile.Injury,
    InjuryMatchesRemaining = profile.InjuryMatchesRemaining,
    ShirtNumber = profile.ShirtNumber,
    MarketValue = profile.MarketValue,
    Salary = profile.Salary,
    WageOnRenewal = profile.WageOnRenewal,
    ContractSeasons = profile.ContractSeasons,
    SeasonsLeft = profile.SeasonsLeft,
    IsInLastSeason = profile.IsInLastSeason,
    AskingPrice = profile.AskingPrice,
    ReleaseCost = profile.ReleaseCost,
    Season = profile.Season.ToDto(),
    Total = profile.Total.ToDto(),
    History = profile.History.Select(line => new PlayerMatchLineDto
    {
        MatchId = line.MatchId,
        SeasonId = line.SeasonId,
        Started = line.Started,
        CameOn = line.CameOn,
        SubbedOff = line.SubbedOff,
        WasOnBenchUnused = line.WasOnBenchUnused,
        Goals = line.Goals,
        OwnGoals = line.OwnGoals,
        Saves = line.Saves,
        Assists = line.Assists,
        Rating = line.Rating,
        RatingBand = line.RatingBand,
        MinutesPlayed = line.MinutesPlayed,
        YellowCards = line.YellowCards,
        RedCards = line.RedCards,
        WasInjured = line.WasInjured,
        InjuredOff = line.InjuredOff,
        IsHome = line.IsHome,
        OpponentName = line.OpponentName,
        OpponentTeamId = line.OpponentTeamId,
        OpponentTeamPrimaryColor = line.OpponentTeamPrimaryColor,
        OpponentTeamSecondaryColor = line.OpponentTeamSecondaryColor,
        HomeGoals = line.HomeGoals,
        AwayGoals = line.AwayGoals,
        RoundNumber = line.RoundNumber,
        TeamName = line.TeamName,
        SeasonName = line.SeasonName,
        CompetitionName = line.CompetitionName,
        PhaseName = line.PhaseName,
        StadiumName = line.StadiumName,
        Attendance = line.Attendance
    }).ToList()
};
}

public static class SquadSuggestionMapping
{
    public static Contracts.SquadSuggestionDto ToDto(this Application.Services.MatchService.SquadSuggestion suggestion) => new()
    {
        StarterIds = suggestion.StarterIds,
        BenchIds = suggestion.BenchIds
    };
}

public static class TeamMatchRecordMapping
{
    public static FinanceLedgerDto ToDto(this Application.Models.FinanceLedger ledger) => new()
    {
        Balance = ledger.Balance,
        Income = ledger.Income,
        Expenses = ledger.Expenses,
        Page = ledger.Page,
        PageSize = ledger.PageSize,
        TotalItems = ledger.TotalItems,
        TotalPages = ledger.TotalPages,
        Movements = ledger.Movements.Select(line => line.ToDto()).ToList()
    };

    public static FinanceMovementDto ToDto(this Application.Models.FinanceLedgerLine line) => new()
    {
        Id = line.Id,
        SeasonId = line.SeasonId,
        SeasonNumber = line.SeasonNumber,
        SeasonName = line.SeasonName,
        MatchDayNumber = line.MatchDayNumber,
        Kind = line.Kind.ToString(),
        Description = line.Description,
        Amount = line.Amount,
        BalanceAfter = line.BalanceAfter,
        StatesABalance = line.StatesABalance
    };

    public static TeamMatchRecordDto ToDto(this Application.Models.TeamMatchRecord record) => new()
    {
        MatchId = record.MatchId,
        OpponentName = record.OpponentName,
        OpponentTeamId = record.OpponentTeamId,
        IsHome = record.IsHome,
        GoalsFor = record.GoalsFor,
        GoalsAgainst = record.GoalsAgainst,
        RoundNumber = record.RoundNumber,
        PlayedAt = record.PlayedAt,
        SeasonName = record.SeasonName,
        CompetitionName = record.CompetitionName,
        PhaseName = record.PhaseName,
        Attendance = record.Attendance,
        StadiumName = record.StadiumName
    };

    public static IReadOnlyList<DivisionPurseDto> ToDtos(this IEnumerable<Application.Models.DivisionPurse> purses) =>
        purses.Select(purse => new DivisionPurseDto
        {
            Tier = purse.Tier,
            Name = purse.Name,
            Purse = purse.Purse,
            Clubs = purse.Clubs,
            Shares = purse.Shares
                .Select(share => new PrizeShareDto { Position = share.Position, Amount = share.Amount })
                .ToList()
        }).ToList();

    public static IReadOnlyList<CupPrizeDto> ToDtos(this IEnumerable<Application.Models.CupPrize> prizes) =>
        prizes
            .Select(prize => new CupPrizeDto
            {
                TieRound = prize.TieRound,
                Name = prize.Name,
                Amount = prize.Amount,
                IsChampion = prize.IsChampion
            })
            .ToList();

    public static PlayerCareerLineDto ToCareerLineDto(this Application.Models.PlayerCareerLine line) => new()
    {
        Appearances = line.Appearances,
        Started = line.Started,
        CameOn = line.CameOn,
        BenchUnused = line.BenchUnused,
        Goals = line.Goals,
        OwnGoals = line.OwnGoals,
        Saves = line.Saves,
        YellowCards = line.YellowCards,
        RedCards = line.RedCards,
        Injuries = line.Injuries,
        MatchesMissed = line.MatchesMissed,
        AverageRating = line.AverageRating,
        RatedMatches = line.RatedMatches
    };

    public static TransferListingDto ToDto(this Application.Models.TransferListing listing) => new()
    {
        PlayerId = listing.PlayerId,
        Name = listing.Name,
        Position = listing.Position,
        Age = listing.Age,
        Speed = listing.Speed,
        Accuracy = listing.Accuracy,
        Dribbling = listing.Dribbling,
        Heading = listing.Heading,
        Strength = listing.Strength,
        GoalkeeperPower = listing.GoalkeeperPower,
        Reflexes = listing.Reflexes,
        Stamina = listing.Stamina,
        Potential = listing.Potential,
        Stars = listing.Stars,
        TeamId = listing.TeamId,
        TeamName = listing.TeamName,
        TeamPrimaryColor = listing.TeamPrimaryColor,
        TeamSecondaryColor = listing.TeamSecondaryColor,
        Energy = listing.Energy,
        Injury = listing.Injury,
        InjuryMatchesRemaining = listing.InjuryMatchesRemaining,
        Retiring = listing.Retiring,
        MarketValue = listing.MarketValue,
        Salary = listing.Salary,
        ContractSeasons = listing.ContractSeasons,
        SeasonsLeft = listing.SeasonsLeft,
        IsInLastSeason = listing.IsInLastSeason,
        AskingPrice = listing.AskingPrice,
        IsFreeAgent = listing.IsFreeAgent,
        HasActiveProposal = listing.HasActiveProposal,
        Season = listing.Season.ToCareerLineDto(),
        Total = listing.Total.ToCareerLineDto(),
        Clubs = listing.Clubs.Select(c => new PlayerClubCareerLineDto
        {
            TeamId = c.TeamId,
            TeamName = c.TeamName,
            Seasons = c.Seasons,
            Total = c.Total.ToCareerLineDto()
        }).ToArray()
    };

    public static TransferProposalDto ToDto(this Application.Models.TransferProposal proposal) => new()
    {
        TransferId = proposal.TransferId,
        PlayerId = proposal.PlayerId,
        PlayerName = proposal.PlayerName,
        PlayerPosition = proposal.PlayerPosition,
        PlayerAge = proposal.PlayerAge,
        SellingClubId = proposal.SellingClubId,
        SellingClubName = proposal.SellingClubName,
        BuyingClubId = proposal.BuyingClubId,
        BuyingClubName = proposal.BuyingClubName,
        ProposalSeasonNumber = proposal.ProposalSeasonNumber,
        ArrivalSeasonNumber = proposal.ArrivalSeasonNumber,
        ArrivalRoundNumber = proposal.ArrivalRoundNumber,
        Fee = proposal.Fee,
        Status = proposal.Status,
        ProposedAt = proposal.ProposedAt,
        ResolvedAt = proposal.ResolvedAt,
        CompletedAt = proposal.CompletedAt
    };

    public static TransferInboxDto ToDto(this Application.Models.TransferInbox inbox) => new()
    {
        ClubId = inbox.ClubId,
        ClubName = inbox.ClubName,
        ProposalSeasonNumber = inbox.ProposalSeasonNumber,
        Incoming = inbox.Incoming.Select(p => p.ToDto()).ToArray(),
        Outgoing = inbox.Outgoing.Select(p => p.ToDto()).ToArray()
    };

    public static TransferSearchResultDto ToDto(this Application.Models.TransferSearchResult result) => new()
    {
        Players = result.Players.Select(p => p.ToDto()).ToArray(),
        Total = result.Total,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalPages = result.TotalPages,
        Window = new TransferWindowStateDto
        {
            SeasonNumber = result.Window.SeasonNumber,
            CurrentRound = result.Window.CurrentRound,
            IsOpen = result.Window.IsOpen,
            ArrivalSeasonNumber = result.Window.ArrivalSeasonNumber,
            ArrivalRoundNumber = result.Window.ArrivalRoundNumber,
            ArrivalLabel = result.Window.ArrivalLabel
        }
    };

    public static TransferHistoryLineDto ToDto(this Application.Models.TransferHistoryLine line) => new()
    {
        TransferId = line.TransferId,
        PlayerId = line.PlayerId,
        PlayerName = line.PlayerName,
        PlayerPosition = line.PlayerPosition,
        SellingClubId = line.SellingClubId,
        SellingClubName = line.SellingClubName,
        BuyingClubId = line.BuyingClubId,
        BuyingClubName = line.BuyingClubName,
        ProposalSeasonNumber = line.ProposalSeasonNumber,
        ArrivalSeasonNumber = line.ArrivalSeasonNumber,
        ArrivalRoundNumber = line.ArrivalRoundNumber,
        Fee = line.Fee,
        Status = Enum.Parse<Domain.Transfers.TransferStatus>(line.Status),
        ProposedAt = line.ProposedAt,
        ResolvedAt = line.ResolvedAt,
        CompletedAt = line.CompletedAt
    };

    public static TransferHistoryLineDto ToDto(this Application.Models.PlayerTransferHistoryLine line) => new()
    {
        TransferId = Guid.Empty,
        PlayerId = line.PlayerId,
        PlayerName = string.Empty,
        PlayerPosition = string.Empty,
        SellingClubId = line.SellingClubId,
        SellingClubName = line.SellingClubName,
        BuyingClubId = line.BuyingClubId,
        BuyingClubName = line.BuyingClubName,
        ProposalSeasonNumber = line.ProposalSeasonNumber,
        ArrivalSeasonNumber = line.ArrivalSeasonNumber,
        ArrivalRoundNumber = null,
        Fee = line.Fee,
        Status = line.Status,
        ProposedAt = line.ProposedAt,
        ResolvedAt = line.ResolvedAt,
        CompletedAt = line.CompletedAt
    };

    public static DivisionRecentTransfersDto ToDto(this Application.Models.DivisionRecentTransfers recent) => new()
    {
        CompetitionSeasonId = recent.CompetitionSeasonId,
        CurrentRound = recent.CurrentRound,
        WindowRounds = recent.WindowRounds,
        Transfers = recent.Transfers.Select(line => line.ToDto()).ToArray()
    };

    public static ClubTransferHistoryDto ToDto(this Application.Models.ClubTransferHistory history) => new()
    {
        TeamId = history.TeamId,
        TeamName = history.TeamName,
        SeasonNumbers = history.SeasonNumbers,
        Transfers = history.Transfers.Select(line => line.ToDto()).ToArray()
    };

    public static ClubBalanceDto ToDto(this Application.Models.ClubBalance balance) => new()
    {
        TeamId = balance.TeamId,
        TeamName = balance.TeamName,
        Balance = balance.Balance
    };

    public static ClubRankingDto ToDto(this Application.Services.ClubRankingEntry entry) => new()
    {
        TeamId = entry.TeamId,
        TeamName = entry.TeamName,
        TeamShortName = entry.TeamShortName,
        PrimaryColor = entry.PrimaryColor,
        SecondaryColor = entry.SecondaryColor,
        ManagerName = entry.ManagerName,
        Position = entry.Position,
        RankingPoints = entry.RankingPoints,
        RankingBase = entry.RankingBase,
        CupScore = entry.CupScore,
        CurrentDivision = entry.CurrentDivision,
        CurrentDivisionName = entry.CurrentDivisionName,
        Strength = entry.Strength,
        SeasonScores = entry.SeasonScores,
        CupScores = entry.CupScores
    };

    public static IReadOnlyList<ClubRankingDto> ToDtos(this IEnumerable<Application.Services.ClubRankingEntry> entries) =>
        entries.Select(entry => entry.ToDto()).ToList();
}

/// <summary>
/// A training sheet, mapped. The prices are carried across rather than recomputed: a cost is
/// a number the domain decided, and mapping is not the place to have an opinion about it.
/// </summary>
public static class TrainingQuoteMapper
{
    public static TrainingQuoteDto ToDto(this TrainingQuote quote) => new()
    {
        PlayerId = quote.PlayerId,
        Name = quote.Name,
        Position = quote.Position,
        Age = quote.Age,
        Potential = quote.Potential,
        Stamina = quote.Stamina,
        Energy = quote.Energy,
        IsAvailable = quote.IsAvailable,
        Injury = quote.Injury,
        SessionFee = quote.SessionFee,
        SessionsLeft = quote.SessionsLeft,
        Attributes = quote.Attributes
            .Select(attribute => new TrainingAttributeQuoteDto
            {
                Attribute = Enum.Parse<Domain.Enums.PlayerAttribute>(attribute.Attribute),
                Value = attribute.Value,
                Cost = attribute.Cost
            })
            .ToList()
    };

    public static SquadTrainingQuotesDto ToDto(this SquadTrainingQuotes quotes) => new()
    {
        TeamId = quotes.TeamId,
        SeasonId = quotes.SeasonId,
        SquadEnergy = quotes.SquadEnergy,
        Day = quotes.Day,
        PlaysToday = quotes.PlaysToday,
        SessionsAllowed = quotes.SessionsAllowed,
        SessionsSpent = quotes.SessionsSpent,
        Players = quotes.Players.Select(player => player.ToDto()).ToList()
    };
}

/// <summary>
/// A club's own page, as the client receives it.
///
/// <para>
/// The mapping is a rename and nothing else: every value the service decided is carried
/// across unchanged. That is the point of putting the assembly in the service — a mapping that
/// worked out a season's name, or a tier, or a division, would be a second place where the
/// facts of a club are decided, and the two would eventually disagree.
/// </para>
/// </summary>
public static class ClubProfileMapping
{
    public static ClubHistoryEventDto ToDto(this ClubHistoryEvent entry) => new()
    {
        Id = entry.Id,
        Kind = entry.Kind,
        SeasonId = entry.SeasonId,
        SeasonNumber = entry.SeasonNumber,
        SeasonName = entry.SeasonName,
        Description = entry.Description,
        Value = entry.Value
    };

    public static ClubTrophyDto ToDto(this ClubTrophy trophy) => new()
    {
        Id = trophy.Id,
        Competition = trophy.Competition,
        Kind = trophy.Kind,
        SeasonId = trophy.SeasonId,
        SeasonNumber = trophy.SeasonNumber,
        SeasonName = trophy.SeasonName,
        DivisionName = trophy.DivisionName,
        DivisionTier = trophy.DivisionTier
    };

    public static ClubProfileDto ToDto(this ClubProfile profile) => new()
    {
        TeamId = profile.TeamId,
        Name = profile.Name,
        ShortName = profile.ShortName,
        PrimaryColor = profile.PrimaryColor,
        SecondaryColor = profile.SecondaryColor,
        CoachName = profile.CoachName,
        SquadSize = profile.SquadSize,
        Balance = profile.Balance,
        Promotions = profile.Promotions,
        Relegations = profile.Relegations,
        Trophies = profile.Trophies.Select(trophy => trophy.ToDto()).ToList(),
        History = profile.History.Select(entry => entry.ToDto()).ToList()
    };
}
