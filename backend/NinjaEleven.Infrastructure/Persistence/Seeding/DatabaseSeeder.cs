using NinjaEleven.Application.Abstractions;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NinjaEleven.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates the starting world: name pools, one season, one competition with all teams
/// enrolled, and a randomly generated squad per team. It never overwrites an existing
/// world: seeding is skipped as soon as a team is already present.
/// </summary>
public class DatabaseSeeder : IDataSeeder
{
    private static readonly (string Name, string ShortName, string Primary, string Secondary)[] TeamCatalog =
    {
        ("Rio Branco Esporte Clube", "RBE", "#B11226", "#F5F5F5"),
        ("Ferroviário Atlético", "FER", "#1B3A6B", "#D4AF37"),
        ("Estrela do Norte", "EDN", "#0F5132", "#FFD700"),
        ("União Serrana", "UNS", "#7B2D8B", "#F0F0F0"),
        ("Porto Marítimo", "PTM", "#00693E", "#1F1F1F"),
        ("Clube Aurora", "CAU", "#E07B00", "#2B2B2B"),
        ("Real Serrano", "RSE", "#0D6EFD", "#FFFFFF"),
        ("Vila Nova do Vale", "VNV", "#8B0000", "#D9D9D9")
    };

    private readonly NinjaElevenDbContext _dbContext;
    private readonly DatabaseSeedOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        NinjaElevenDbContext dbContext,
        IOptions<DatabaseSeedOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedNamePoolsAsync(cancellationToken);

        if (await _dbContext.Teams.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Seeding skipped: the world already contains teams.");
            return;
        }

        var random = _options.RandomSeed.HasValue
            ? new Random(_options.RandomSeed.Value)
            : Random.Shared;

        var season = CreateSeason();
        var competition = Competition.Create("Campeonato Brasileiro", CompetitionType.League);
        var competitionSeason = CompetitionSeason.Create(competition.Id, season.Id);
        var startDate = season.StartDate;

        var participants = new List<CompetitionParticipant>();
        var teams = new List<Team>();
        var players = new List<Player>();
        var seasonStates = new List<PlayerSeasonState>();
        var memberships = new List<TeamMembership>();

        var teamCount = _options.Teams;
        if (teamCount < 1 || teamCount > TeamCatalog.Length)
        {
            throw new InvalidOperationException(
                $"The team catalog holds {TeamCatalog.Length} teams but {teamCount} were requested.");
        }

        for (var index = 0; index < teamCount; index++)
        {
            var definition = TeamCatalog[index];
            var team = CreateTeam(definition, random);

            teams.Add(team);
            participants.Add(CompetitionParticipant.Create(competitionSeason.Id, team.Id));

            var squad = CreateSquad(team, season.Id, startDate, random);

            players.AddRange(squad.Players);
            seasonStates.AddRange(squad.SeasonStates);
            memberships.AddRange(squad.Memberships);
        }

        _dbContext.Seasons.Add(season);
        _dbContext.Competitions.Add(competition);
        _dbContext.CompetitionSeasons.Add(competitionSeason);
        _dbContext.CompetitionParticipants.AddRange(participants);
        _dbContext.Teams.AddRange(teams);
        _dbContext.Players.AddRange(players);
        _dbContext.PlayerSeasonStates.AddRange(seasonStates);
        _dbContext.TeamMemberships.AddRange(memberships);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded season {SeasonName} with {TeamCount} teams and {PlayerCount} players.",
            season.Name,
            teams.Count,
            players.Count);
    }

    private async Task SeedNamePoolsAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.Names.AnyAsync(cancellationToken))
        {
            _dbContext.Names.AddRange(NameCatalog.AllFirstNames.Select(Name.Create));
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} first names.", NameCatalog.AllFirstNames.Count);
        }

        if (!await _dbContext.Surnames.AnyAsync(cancellationToken))
        {
            _dbContext.Surnames.AddRange(NameCatalog.AllSurnames.Select(Surname.Create));
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} surnames.", NameCatalog.AllSurnames.Count);
        }
    }

    private static Season CreateSeason()
    {
        var season = Season.Create("2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        return season;
    }

    private static Team CreateTeam(
        (string Name, string ShortName, string Primary, string Secondary) definition,
        Random random)
    {
        return Team.Create(
            definition.Name,
            definition.ShortName,
            definition.Primary,
            definition.Secondary,
            random.Next(50, 91));
    }

    private (List<Player> Players, List<PlayerSeasonState> SeasonStates, List<TeamMembership> Memberships) CreateSquad(
        Team team,
        Guid seasonId,
        DateOnly startDate,
        Random random)
    {
        var players = new List<Player>();
        var seasonStates = new List<PlayerSeasonState>();
        var memberships = new List<TeamMembership>();

        foreach (var position in BuildPositions(_options.PlayersPerTeam, _options.GoalkeepersPerTeam))
        {
            var player = CreatePlayer(position, random);
            var state = PlayerSeasonState.Create(
                player.Id,
                seasonId,
                team.Id,
                random.Next(_options.MinimumEnergy, _options.MaximumEnergy + 1));

            players.Add(player);
            seasonStates.Add(state);
            memberships.Add(TeamMembership.Create(player.Id, team.Id, startDate));
        }

        return (players, seasonStates, memberships);
    }

    private Player CreatePlayer(Position position, Random random)
    {
        var firstName = NameCatalog.AllFirstNames[random.Next(NameCatalog.AllFirstNames.Count)];
        var surname = NameCatalog.AllSurnames[random.Next(NameCatalog.AllSurnames.Count)];

        var birthYear = random.Next(_options.OldestBirthYear, _options.YoungestBirthYear + 1);
        var birthMonth = random.Next(1, 13);
        var birthDay = random.Next(1, DateTime.DaysInMonth(birthYear, birthMonth) + 1);
        var birthDate = new DateOnly(birthYear, birthMonth, birthDay);

        // A goalkeeper is built for his job: the engine weighs reflexes and power when it
        // picks the eleven, so a keeper with outfield attributes would never be chosen.
        var isGoalkeeper = position == Position.GK;

        return Player.Create(
            $"{firstName} {surname}",
            birthDate,
            position,
            speed: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            accuracy: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            dribbling: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            heading: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            strength: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            goalkeeperPower: isGoalkeeper ? StrongAttribute(random) : 0,
            reflexes: isGoalkeeper ? StrongAttribute(random) : 0);
    }

    /// <summary>
    /// A squad needs a couple of goalkeepers, not only the one that starts, and the rest
    /// of the slots are spread over DEF, MID and ATT as evenly as possible. Lineup rules
    /// are domain rules, but the shape of a roster is a data concern, so it lives here.
    /// </summary>
    private IEnumerable<Position> BuildPositions(int playersPerTeam, int goalkeepers)
    {
        if (playersPerTeam < 1)
        {
            throw new InvalidOperationException("A squad needs at least one player.");
        }

        // Never more goalkeepers than players, and always at least the one the lineup
        // rules demand.
        var keeperCount = Math.Clamp(goalkeepers, 1, playersPerTeam);

        for (var slot = 0; slot < keeperCount; slot++)
        {
            yield return Position.GK;
        }

        var outfield = playersPerTeam - keeperCount;
        var groups = new[] { Position.DEF, Position.MID, Position.ATT };
        var baseSize = outfield / groups.Length;
        var remainder = outfield % groups.Length;

        for (var group = 0; group < groups.Length; group++)
        {
            var size = baseSize + (group < remainder ? 1 : 0);
            for (var slot = 0; slot < size; slot++)
            {
                yield return groups[group];
            }
        }
    }

    private int RandomAttribute(Random random) =>
        random.Next(_options.MinimumAttribute, _options.MaximumAttribute + 1);

    /// <summary>
    /// An outfield player, in the upper half of the attribute range: the clubs are seeded
    /// so a starting eleven is worth watching.
    /// </summary>
    private int OutfieldAttribute(Random random)
    {
        var low = _options.MinimumAttribute + (_options.MaximumAttribute - _options.MinimumAttribute) / 2;
        return random.Next(low, _options.MaximumAttribute + 1);
    }

    /// <summary>
    /// A goalkeeper's own attributes, well above what an outfield player reaches, so the
    /// best keeper of a club is recognisable.
    /// </summary>
    private int StrongAttribute(Random random)
    {
        var low = _options.MinimumAttribute + 3 * (_options.MaximumAttribute - _options.MinimumAttribute) / 4;
        return random.Next(Math.Min(low, _options.MaximumAttribute), _options.MaximumAttribute + 1);
    }
}
