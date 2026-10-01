using NinjaEleven.Application.Abstractions;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NinjaEleven.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates the starting world: name pools, one season, four divisions with 16 clubs each,
/// a cup with all 64 clubs, and a Supercup. Clubs are assigned to divisions by strength.
/// </summary>
public class DatabaseSeeder : IDataSeeder
{
    private static readonly (string Name, string ShortName, string Primary, string Secondary)[] TeamCatalog =
    {
        // 16 clubs for initial generation (will be sorted by strength into 4 divisions)
        ("Rio Branco Esporte Clube", "RBE", "#B11226", "#F5F5F5"),
        ("Ferroviário Atlético", "FER", "#1B3A6B", "#D4AF37"),
        ("Estrela do Norte", "EDN", "#0F5132", "#FFD700"),
        ("União Serrana", "UNS", "#7B2D8B", "#F0F0F0"),
        ("Porto Marítimo", "PTM", "#00693E", "#1F1F1F"),
        ("Clube Aurora", "CAU", "#E07B00", "#2B2B2B"),
        ("Real Serrano", "RSE", "#0D6EFD", "#FFFFFF"),
        ("Vila Nova do Vale", "VNV", "#8B0000", "#D9D9D9"),
        ("Atlético do Planalto", "ADP", "#004B8D", "#FFD700"),
        ("Grêmio Litorâneo", "GLI", "#0B6623", "#F5F5F5"),
        ("Esporte Clube Serra Azul", "SAZ", "#5C2D91", "#FFFFFF"),
        ("Rio Pardo Futebol Clube", "RPF", "#C8102E", "#000000"),
        ("Nacional Serranense", "NSE", "#1E6F5C", "#F0E68C"),
        ("Clube Atlético Barreiras", "CAB", "#0F4C81", "#FFD700"),
        ("Esporte Clube Laranjeiras", "ECL", "#E85A0C", "#2B2B2B"),
        ("Grêmio Ferroviário do Sul", "GFS", "#37474F", "#E53935"),
        ("Atlético Bandeirante", "ATB", "#2E7D32", "#FFFFFF"),
        ("Sociedade Esportiva Cerradão", "SEC", "#6A1B9A", "#F5F5F5"),
        ("União Atlético Maravilha", "UAM", "#00838F", "#FFEB3B"),
        ("Clube Náutico Ipanema", "CNI", "#0277BD", "#FFFFFF"),
        ("Esporte Clube Palmeiral", "EPL", "#EF6C00", "#1B5E20"),
        ("Grêmio Esportivo Andorinha", "GEA", "#455A64", "#FFCA28"),
        ("Clube Atlético Santa Clara", "CSC", "#7B1FA2", "#F5F5F5"),
        ("Sport Club Interface", "SCI", "#212121", "#00E5FF"),
        ("Associação Atlética Guarani", "AAG", "#1565C0", "#FFFFFF"),
        ("Clube Esportivo Tijuco", "CET", "#2E7D32", "#212121"),
        ("Grêmio Operário Seridoense", "GOS", "#4527A0", "#FFD700"),
        ("Esporte Clube Riachuelo", "ECR", "#AD1457", "#F5F5F5"),
        ("Sociedade Recreativa Estância", "SRE", "#00695C", "#FF8F00"),
        ("Clube Atlético Juazeirense", "CAJ", "#283593", "#FFFFFF"),
        ("Grêmio Esportivo Várzea Nova", "GVN", "#33691E", "#F5F5F5"),
        ("Sport Club Aurora Sul", "SAS", "#5D4037", "#FFD54F"),
        ("Associação Esportiva Cristal", "AEC", "#00838F", "#263238"),
        ("Clube Esportivo Umbuzeiro", "CEU", "#9E9D24", "#FFFFFF"),
        ("Grêmio Atlético Potiguar", "GAP", "#C62828", "#FFFFFF"),
        ("Esporte Clube Dourado", "ECD", "#F9A825", "#212121"),
        ("Atlético do Interior", "ADI", "#2E7D32", "#FFFFFF"),
        ("Clube Esportivo Primavera", "CEP", "#1B5E20", "#FFD700"),
        ("Grêmio Atlético Metrópole", "GAM", "#0D47A1", "#FFFFFF"),
        ("Esporte Clube Horizonte", "ECH", "#00695C", "#FFB300"),
        ("Sociedade Esportiva Alvorada", "SEA", "#E65100", "#FFFFFF"),
        ("União Desportiva Capital", "UDC", "#37474F", "#FFEB3B"),
        ("Clube Atlético Pioneiro", "CAP", "#1A237E", "#FFFFFF"),
        ("Grêmio Esportivo Vitória", "GEV", "#BF360C", "#FFD700"),
        ("Esporte Clube Independência", "ECI", "#004D40", "#FFFFFF"),
        ("Associação Atlética Progresso", "AAP", "#4A148C", "#FFD700"),
        ("Clube Esportivo Liberdade", "CEL", "#B71C1C", "#FFFFFF"),
        ("Grêmio Atlético União", "GAU", "#01579B", "#FFD700"),
        ("Esporte Clube Futuro", "ECF", "#311B92", "#FFFFFF"),
        ("Sociedade Esportiva Renascença", "SER", "#880E4F", "#FFD700"),
        ("União Desportiva Conquista", "UDC2", "#1B5E20", "#FFFFFF"),
        ("Clube Atlético Tradição", "CAT", "#4E342E", "#FFD700"),
        ("Grêmio Esportivo Esperança", "GEE", "#0D47A1", "#FFD700"),
        ("Esporte Clube Harmonia", "ECH2", "#2E7D32", "#FFFFFF"),
        ("Associação Atlética Glória", "AAG2", "#C62828", "#FFD700"),
        ("Clube Esportivo União", "CEU2", "#37474F", "#FFFFFF"),
        ("Grêmio Atlético Magnífico", "GAM2", "#4A148C", "#FFD700"),
        ("Esporte Clube Suprema", "ECS", "#BF360C", "#FFFFFF"),
        ("Sociedade Esportiva Diamante", "SED", "#01579B", "#FFD700"),
        // 5 more to reach 64 clubs
        ("Esporte Clube Aurora Boreal", "ECAB", "#1DE9B6", "#000000"),
        ("Atlético Vale do Sol", "AVS", "#FFD600", "#000000"),
        ("Grêmio Esportivo Lua Nova", "GELN", "#9E9E9E", "#000000"),
        ("Clube Atlético Raio", "CAR", "#FF6D00", "#FFFFFF"),
        ("Sociedade Esportiva Estrela", "SEE", "#FFFF00", "#000000"),
    };

    private static readonly string[] CoachNames =
    {
        "Carlos Alberto Parreira",
        "Vanderlei Luxemburgo",
        "Luiz Felipe Scolari",
        "Tite",
        "Abel Ferreira",
        "Jorge Jesus",
        "Renato Gaúcho",
        "Cuca",
        "Dorival Júnior",
        "Felipe Conceição",
        "Rogério Ceni",
        "Fernando Diniz",
        "Eduardo Coudet",
        "Paulo Autuori",
        "Guto Ferreira",
        "Lisca",
        "Zé Ricardo",
        "Enderson Moreira",
        "Cláudio Tencati",
        "Hemerson Maria",
        "Jorginho",
        "Mozart Santos",
        "Rodrigo Chagas",
        "Tché Tché",
        "Bruno Pivetti",
        "Alexandre Gallo",
        "Ricardo Drubscky",
        "Sérgio Soares",
        "Argel Fuchs",
        "Mazola Júnior",
        "Dado Cavalcanti",
        "Léo Condé",
        "Ranielle Ribeiro",
        "Higo Magalhães",
        "Allan Aal",
        "Márcio Fernandes",
        "Paulo Pezzolano",
        "Eduardo Barroca",
        "Jair Ventura",
        "Marquinhos Santos",
        "Gilson Kleina",
        "Roberto Fonseca",
        "Hemerson Maria",
        "Wagner Lopes",
        "Daniel Paulista",
        "Mozart Santos",
        "Cristóvão Borges",
        "Gilmar Dal Pozzo",
        "Rogério Zimmermann",
        // Additional 15 names to reach 64
        "Antônio Lopes",
        "Joel Santana",
        "Celso Roth",
        "Sérgio Guedes",
        "Paulo Comelli",
        "Roberto Cavalo",
        "Tuca Guimarães",
        "Vica",
        "Pintado",
        "Sérgio Ramirez",
        "Ricardo Severo",
        "Alexandre Grasseli",
        "Marcelo Veiga",
        "Edson Gaúcho",
        "Luiz Carlos Cruz",
    };

    private static readonly (string Name, string Industry, string Color)[] SponsorCatalog =
    {
        ("Cia. Energética Paulista", "Energia", "#E0A800"),
        ("Banco do Vale", "Financeiro", "#2E7D32"),
        ("Rede Ferrovia do Sul", "Transportes", "#1565C0"),
        ("Construtora Rocha & Filhos", "Construção", "#6A1B9A"),
        ("Cooperativa Aurora", "Alimentos", "#AD1457"),
        ("Telecom Nordeste", "Telecomunicações", "#00838F"),
        ("Grupo Martelo", "Varejo", "#F45118"),
        ("Petrobraz", "Petróleo", "#00695C"),
        ("Mineração Serra", "Mineração", "#4E3482"),
        ("Agropecuária Verde", "Agronegócio", "#388E3C"),
        ("Aviação Regional", "Aéreo", "#0277BD"),
        ("Celular Sul", "Telecomunicações", "#00857A"),
        ("Cervejaria Lager", "Bebidas", "#FFD700"),
        ("Cosméticos Bella", "Beleza", "#C2185B"),
        ("Indústria de Plásticos", "Manufatura", "#5D4037"),
        ("Logística Expressa", "Transportes", "#EF6C00"),
        ("Mecânica dos Campos", "Automotivo", "#455A64"),
        ("Miniaturas Fantasy", "Lazer", "#3949AB"),
        ("Pet Shop Felino", "Varejo", "#EC0000"),
        ("Tech Solutions", "Tecnologia", "#00B0FF"),
        ("Construtora Ponte Alta", "Construção", "#795548"),
        ("Laticínios Vale Verde", "Alimentos", "#8BC34A"),
        ("Transportes União", "Transportes", "#607D8B"),
        ("Seguros Confiança", "Financeiro", "#3F51B5"),
        ("Energia Solar Brasil", "Energia", "#FFC107"),
        ("Tecnologia Avançada", "Tecnologia", "#9C27B0"),
        ("Madeira Nobre", "Manufatura", "#5D4037"),
        ("Rede Hospitalar Vida", "Saúde", "#E91E63"),
        ("Editora Cultura", "Mídia", "#673AB7"),
        ("Auto Peças Central", "Automotivo", "#455A64"),
    };

    private readonly NinjaElevenDbContext _dbContext;
    private readonly DatabaseSeedOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    /// <summary>
    /// A seed offset for stamina's own stream, so it cannot collide with the world's.
    /// </summary>
    private const int StaminaStreamSalt = 7_919;

    /// <summary>
    /// A seed offset for potential's own stream, kept apart from the world's and from
    /// stamina's. The rule is the one above and it is not negotiable: every draw a player
    /// creation makes beyond the world's own attributes comes from a stream of its own, or
    /// adding an attribute rewrites a world that already exists.
    /// </summary>
    private const int PotentialStreamSalt = 5_153;

    /// <summary>
    /// Stamina is drawn from a stream of its own, and that is a rule about the seeder rather
    /// than about bodies.
    ///
    /// Every other attribute is drawn from the one shared stream, and drawing from a shared
    /// stream means a single extra draw rewrites everything drawn after it. Adding stamina to
    /// the player did exactly that: one more <c>Next</c> per man, and the world's faces,
    /// names, positions and attribute values all shifted with it. A season's intake came out
    /// of it with twenty-seven per cent of its men in goal rather than the fifteen per cent
    /// the intake rule asks for — nothing had been changed about the rule, and the rule was
    /// still being broken.
    ///
    /// The cost of a shared stream is that the world a seed produces is only stable while the
    /// list of things being drawn from it is frozen, which is the opposite of what a seeder
    /// is for. A separate stream is a draw the world does not see, so an attribute can be
    /// added without rewriting a world that already exists. It also means the position roll
    /// and the stamina roll are independent, which is what a keeper's tank and a
    /// centre-back's tank being unrelated should mean.
    /// </summary>
    private readonly Random _staminaRandom;

    /// <summary>
    /// The second of the two private streams, and it exists because the first was not
    /// enough. Stamina moved off the shared stream and the world settled; potential was then
    /// added and drawn from the shared stream, and the intake came out of it with
    /// twenty-seven per cent of its men in goal against a fifteen per cent rule again —
    /// the same failure, the same test, three commits after the rule was written down in this
    /// file. A rule that only the thing that motivated it follows is not a rule, so this
    /// stream exists as a second instance of the same decision rather than as a special case.
    /// </summary>
    private readonly Random _potentialRandom;

    public DatabaseSeeder(
        NinjaElevenDbContext dbContext,
        IOptions<DatabaseSeedOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;

        _staminaRandom = _options.RandomSeed.HasValue
            ? new Random(StaminaStreamSalt + _options.RandomSeed.Value)
            : Random.Shared;

        _potentialRandom = _options.RandomSeed.HasValue
            ? new Random(PotentialStreamSalt + _options.RandomSeed.Value)
            : Random.Shared;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedNamePoolsAsync(cancellationToken);
        await SeedSponsorsAsync(cancellationToken);

        var random = _options.RandomSeed.HasValue
            ? new Random(_options.RandomSeed.Value)
            : Random.Shared;

        if (await _dbContext.Teams.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Seeding skipped: the world already contains teams.");
            await GiveFacesToTheWorldAlreadySeededAsync(random, cancellationToken);
            await GiveIdentitiesToTheWorldAlreadySeededAsync(cancellationToken);
            await GiveManagersToTheWorldAlreadySeededAsync(random, cancellationToken);
            await OpenTheBooksOfTheWorldAlreadySeededAsync(cancellationToken);
            return;
        }

        await SeedStartingWorldAsync(random, cancellationToken);
        await GiveFacesToTheWorldAlreadySeededAsync(random, cancellationToken);
        await GiveIdentitiesToTheWorldAlreadySeededAsync(cancellationToken);
        await GiveManagersToTheWorldAlreadySeededAsync(random, cancellationToken);
        await OpenTheBooksOfTheWorldAlreadySeededAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the starting world for a brand-new database: one season, four divisions,
    /// a cup with all 64 clubs, a Supercup, 64 clubs with squads, and books opened for each.
    /// Clubs are assigned to divisions by strength after squad generation.
    /// </summary>
    private async Task SeedStartingWorldAsync(Random random, CancellationToken cancellationToken)
    {
        var season = CreateSeason();
        var startDate = season.StartDate;

        var divisions = CompetitionRules.Tiers()
            .Select(tier => Division.Create(null, tier))
            .ToList();

        var championship = Competition.Create("Campeonato Brasileiro", CompetitionType.League);
        var cup = Competition.Create("Copa do Brasil", CompetitionType.Cup);
        var superCup = Competition.Create("Supercopa Nacional", CompetitionType.SuperCup);

        var leagueEditions = divisions
            .Select(division => CompetitionSeason.Create(championship.Id, season.Id, division.Id))
            .ToList();

        var cupEdition = CompetitionSeason.Create(cup.Id, season.Id);
        var superCupEdition = CompetitionSeason.Create(superCup.Id, season.Id);

        var teams = new List<Team>();
        var stadiums = new List<Stadium>();
        var players = new List<Player>();
        var seasonStates = new List<PlayerSeasonState>();
        var memberships = new List<TeamMembership>();
        var movements = new List<FinanceMovement>();
        var managers = new List<Manager>();

        var totalPlayers = TeamCatalog.Length * _options.PlayersPerTeam;
        var faces = DealFaces(totalPlayers, random);
        var coachPool = CoachNames.OrderBy(_ => random.Next()).ToList();

        if (coachPool.Count < TeamCatalog.Length)
        {
            throw new InvalidOperationException(
                $"The coach pool has {coachPool.Count} names for {TeamCatalog.Length} clubs. " +
                "Every club needs its own manager, so the pool has to cover the catalog.");
        }

        // First, create all teams with squads and calculate their strengths
        var teamStrengths = new List<(Team Team, double Strength, int CoachIndex)>();

        for (var index = 0; index < TeamCatalog.Length; index++)
        {
            var definition = TeamCatalog[index];
            var team = CreateTeam(definition, random);
            var stadium = Stadium.Create(team.Id, definition.Name);

            teams.Add(team);
            stadiums.Add(stadium);
            team.SetStadium(stadium);

            movements.Add(FinanceMovement.Seed(team.Id, season.Id, FinanceRules.StartingBalance));

            var coachName = coachPool[index];
            managers.Add(Manager.Create(team.Id, coachName));

            var squad = CreateSquad(team, season.Id, season.Number, startDate, random, faces);

            players.AddRange(squad.Players);
            seasonStates.AddRange(squad.SeasonStates);
            memberships.AddRange(squad.Memberships);

            // Calculate club strength from full squad
            var strength = PlayerRating.CalculateTeamStars(squad.Players);
            teamStrengths.Add((team, strength, index));
        }

        // Sort clubs by strength (strongest first) and assign to divisions
        // Top 16 -> 1st Division, 17-32 -> 2nd Division, 33-48 -> 3rd Division, 49-64 -> 4th Division
        var sortedByStrength = teamStrengths.OrderByDescending(t => t.Strength).ToList();
        var participants = new List<CompetitionParticipant>();

        for (var i = 0; i < sortedByStrength.Count; i++)
        {
            var (team, strength, coachIndex) = sortedByStrength[i];
            var tier = (i / CompetitionRules.ClubsPerDivision) + 1; // 1-4
            var edition = leagueEditions[tier - 1];
            
            participants.Add(CompetitionParticipant.Create(edition.Id, team.Id));
            
            _logger.LogInformation("Club {ClubName} assigned to {Division} (strength: {Strength:F2})", 
                team.Name, CompetitionRules.DivisionName(tier), strength);
        }

        _dbContext.Seasons.Add(season);
        _dbContext.Divisions.AddRange(divisions);
        _dbContext.Competitions.AddRange(championship, cup, superCup);
        _dbContext.CompetitionSeasons.AddRange(leagueEditions);
        _dbContext.CompetitionSeasons.AddRange(cupEdition, superCupEdition);
        _dbContext.CompetitionParticipants.AddRange(participants);
        _dbContext.Teams.AddRange(teams);
        _dbContext.Stadiums.AddRange(stadiums);
        _dbContext.Managers.AddRange(managers);
        _dbContext.Players.AddRange(players);
        _dbContext.PlayerSeasonStates.AddRange(seasonStates);
        _dbContext.TeamMemberships.AddRange(memberships);
        _dbContext.FinanceMovements.AddRange(movements);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // The first season is a season like any other, and it opens with its intake: the young
        // free agents a club signs rather than buys.
        var intake = await AddYoungFreeAgentsAsync(
            season.Id, YouthIntakeRules.FreeAgentsPerSeason, random, cancellationToken);

        _logger.LogInformation(
            "Seeded {SeasonName}: {DivisionCount} divisions of {ClubsPerDivision} clubs, a cup of {CupSize} and a Supercup, with {TeamCount} teams, {PlayerCount} players and an intake of {Intake} young free agents.",
            season.Name,
            CompetitionRules.DivisionCount,
            CompetitionRules.ClubsPerDivision,
            CompetitionRules.CupSize,
            teams.Count,
            players.Count,
            intake);
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

    /// <summary>
    /// The master sponsor catalog: the companies that can appear on a shirt. Seeded only
    /// once, so a world re-seeded keeps its sponsors and a sponsor that has been signed by a
    /// club is the same row it always was.
    /// </summary>
    private async Task SeedSponsorsAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Sponsors.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.Sponsors.AddRange(SponsorCatalog.Select(
            sponsor => Sponsor.Create(sponsor.Name, sponsor.Industry, sponsor.Color)));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} sponsors.", SponsorCatalog.Length);
    }

    /// <summary>
    /// The first season of a new world: today, and the thirty-four days a season lasts.
    ///
    /// <para>
    /// The date is today rather than a fixed one in the past because a matchday is a day and the
    /// Scheduler asks the calendar what is due. A world whose first season began nine months ago
    /// is a world that owes its whole season on its first morning, and the world answers that by
    /// playing every matchday it missed in a single pass — which is what "the scheduler skipped
    /// from the first round to the fourth" looks like from the other side. Starting the season
    /// where the world was written is what makes the first round the first round.
    /// </para>
    /// </summary>
    private static Season CreateSeason()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var season = Season.Create(1, today, today.AddDays(CompetitionRules.SeasonMatchDays - 1));
        season.Start();
        return season;
    }

    private static Team CreateTeam(
        (string Name, string ShortName, string Primary, string Secondary) definition,
        Random random)
    {
        var team = Team.Create(
            definition.Name,
            definition.ShortName,
            definition.Primary,
            definition.Secondary);

        // A new world is given every club a badge and two shirts before a manager has been asked
        // anything, because a club that has to be drawn before it can be shown is a club that is
        // a blank space in every table on the way there.
        team.SetCrest(ClubIdentityDefaults.CrestFor(team));
        team.SetHomeKit(ClubIdentityDefaults.HomeKitFor(team));
        team.SetAwayKit(ClubIdentityDefaults.AwayKitFor(team));

        return team;
    }

    /// <summary>
    /// Gives a crest and two shirts to every club that has none.
    ///
    /// The same thing the faces do and for the same reason: the seeder skips a world that
    /// already has teams, so a world written before kits existed would keep its clubs in two
    /// colours and nothing else for ever. A club that has already been drawn by its manager is
    /// left alone — the default fills a gap and never overwrites a choice.
    /// </summary>
    private async Task GiveIdentitiesToTheWorldAlreadySeededAsync(CancellationToken cancellationToken)
    {
        var bare = await _dbContext.Teams
            .Where(team => team.CrestJson == null || team.HomeKitJson == null || team.AwayKitJson == null)
            .ToListAsync(cancellationToken);

        if (bare.Count == 0)
        {
            return;
        }

        foreach (var team in bare)
        {
            if (team.CrestJson is null)
            {
                team.SetCrest(ClubIdentityDefaults.CrestFor(team));
            }

            if (team.HomeKitJson is null)
            {
                team.SetHomeKit(ClubIdentityDefaults.HomeKitFor(team));
            }

            if (team.AwayKitJson is null)
            {
                team.SetAwayKit(ClubIdentityDefaults.AwayKitFor(team));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Gave a crest and two shirts to {Count} clubs that had none.", bare.Count);
    }

    private (List<Player> Players, List<PlayerSeasonState> SeasonStates, List<TeamMembership> Memberships) CreateSquad(
        Team team,
        Guid seasonId,
        int startSeasonNumber,
        DateOnly startDate,
        Random random,
        Queue<string> faces)
    {
        var players = new List<Player>();
        var seasonStates = new List<PlayerSeasonState>();
        var memberships = new List<TeamMembership>();

        foreach (var position in BuildPositions(_options.PlayersPerTeam, _options.GoalkeepersPerTeam))
        {
            var player = CreatePlayer(position, random, faces.Dequeue());
            var state = PlayerSeasonState.Create(
                player.Id,
                seasonId,
                team.Id,
                random.Next(_options.MinimumEnergy, _options.MaximumEnergy + 1));

            players.Add(player);
            seasonStates.Add(state);
            memberships.Add(TeamMembership.Create(player.Id, team.Id, startDate, startSeasonNumber: startSeasonNumber));
        }

        return (players, seasonStates, memberships);
    }

    private Player CreatePlayer(Position position, Random random, string face) =>
        CreatePlayer(position, random, face, random.Next(_options.YoungestAge, _options.OldestAge + 1));

    /// <summary>
    /// Creates a player with attributes generated from the probability distribution:
    /// 1-10 (5%), 11-20 (10%), 21-40 (20%), 41-60 (30%), 61-80 (20%), 81-90 (10%), 91-100 (5%)
    /// </summary>
    private Player CreatePlayer(Position position, Random random, string face, int age)
    {
        var firstName = NameCatalog.AllFirstNames[random.Next(NameCatalog.AllFirstNames.Count)];
        var surname = NameCatalog.AllSurnames[random.Next(NameCatalog.AllSurnames.Count)];

        var isGoalkeeper = position == Position.GK;

        // The attributes are drawn into locals first so the potential can be read off the very
        // attributes this man is being given. Drawing a second set to measure him with would
        // rate a player on a player who does not exist, and the two readings would disagree
        // often enough to matter and rarely enough to be found by reading the code.
        var speed = GenerateAttribute(random);
        var accuracy = GenerateAttribute(random);
        var dribbling = GenerateAttribute(random);
        var heading = GenerateAttribute(random);
        var strength = GenerateAttribute(random);
        var goalkeeperPower = isGoalkeeper ? GenerateAttribute(random) : 0;
        var reflexes = isGoalkeeper ? GenerateAttribute(random) : 0;

        var stamina = GenerateStamina(age);

        return Player.Create(
            $"{firstName} {surname}",
            age,
            position,
            speed: speed,
            accuracy: accuracy,
            dribbling: dribbling,
            heading: heading,
            strength: strength,
            goalkeeperPower: goalkeeperPower,
            reflexes: reflexes,
            face: face,
            stamina: stamina,
            potential: GeneratePotential(random, age, position, speed, accuracy, dribbling, heading, strength, goalkeeperPower, reflexes));
    }

    /// <summary>
    /// The ceiling of a newly drawn man: his own current reading, the top a body of his age
    /// could reach, and a draw biased low so that most men are near their ceiling and a few
    /// are far above it.
    ///
    /// <para>
    /// It is read off the attributes the man was just given and not rolled beside them, which
    /// is the only way the number means anything: a potential drawn independently of the
    /// player would have had a good twenty-three-year-old capped below where he already is,
    /// and the world's best young players would have been the ones the dice happened to pair
    /// with a high roll.
    /// </para>
    /// </summary>
    private int GeneratePotential(
        Random random,
        int age,
        Position position,
        int speed,
        int accuracy,
        int dribbling,
        int heading,
        int strength,
        int goalkeeperPower,
        int reflexes)
    {
        var reading = CurrentReading(
            position, speed, accuracy, dribbling, heading, strength, goalkeeperPower, reflexes);

        return DevelopmentRules.PotentialFor(reading, age, _potentialRandom.NextDouble());
    }

    /// <summary>
    /// The weighted reading of a man who does not exist yet, from the attributes he was just
    /// given. It repeats <see cref="DevelopmentRules.Overall"/> rather than calling it because
    /// there is no player to read: the reading is what the potential is drawn from, so the man
    /// has to exist before the rule can be asked about him.
    /// </summary>
    private static int CurrentReading(
        Position position,
        int speed,
        int accuracy,
        int dribbling,
        int heading,
        int strength,
        int goalkeeperPower,
        int reflexes)
    {
        if (position == Position.GK)
        {
            return (goalkeeperPower + reflexes) / 2;
        }

        var weights = AttributeWeights.For(position);

        var reading =
            speed * weights.Speed
            + accuracy * weights.Accuracy
            + dribbling * weights.Dribbling
            + heading * weights.Heading
            + strength * weights.Strength;

        return (int)Math.Round(reading, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Generates an attribute value (1-100) using the probability distribution:
    /// 1-10: 5%, 11-20: 10%, 21-40: 20%, 41-60: 30%, 61-80: 20%, 81-90: 10%, 91-100: 5%
    /// </summary>
    private int GenerateAttribute(Random random)
    {
        var roll = random.NextDouble();
        
        // Select range based on probability
        int min, max;
        if (roll < 0.05)
        {
            min = 1; max = 10; // 5%
        }
        else if (roll < 0.15)
        {
            min = 11; max = 20; // 10%
        }
        else if (roll < 0.35)
        {
            min = 21; max = 40; // 20%
        }
        else if (roll < 0.65)
        {
            min = 41; max = 60; // 30%
        }
        else if (roll < 0.85)
        {
            min = 61; max = 80; // 20%
        }
        else if (roll < 0.95)
        {
            min = 81; max = 90; // 10%
        }
        else
        {
            min = 91; max = 100; // 5%
        }

        return random.Next(min, max + 1);
    }

    /// <summary>
    /// How much a body of this age has in the tank, as a starting point rather than a rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stamina is drawn on its own curve rather than as another <see cref="GenerateAttribute"/>
    /// call, because it is not one thing a player is good at and it is not spread across the
    /// world the way skill is: it peaks in the middle of a career and is bounded by what a
    /// body can carry.
    /// </para>
    /// <para>
    /// The bands are the development curve's own shape, read at five points along it, and the
    /// peak sits on <see cref="DevelopmentRules.StaminaPeakAge"/> because that is where the
    /// rule says a body is fullest. They are a starting point and not a rule: from the next
    /// season this man fills, peaks and empties on the curve, and a world that drew a
    /// nineteen-year-old's tank from a different peak than the one his own development used
    /// would have had him grow and decline against a curve he was not on.
    /// </para>
    /// </remarks>
    private int GenerateStamina(int age)
    {
        // The bands are read off the curve itself — a body that has been filling since the
        // youngest age, evaluated at each of these ages — so a man the world draws at twenty
        // and a man it draws at twenty-six are on the same curve rather than on two that
        // happen to look similar. Bands invented separately drift: a world whose seed said
        // ninety at twenty-six and whose rule topped out at seventy-two would have had every
        // veteran seeded above the ceiling his own development could reach, and the ceiling
        // would have been a number nothing in the world was ever near.
        var baseStamina = age switch
        {
            <= 17 => 64,
            <= 20 => 83,
            <= 23 => 87,
            <= 26 => 88,
            <= 29 => 80,
            <= 32 => 68,
            <= 35 => 57,
            <= 38 => 47,
            <= 42 => 38,
            _ => 30
        };

        return Math.Clamp(baseStamina + _staminaRandom.Next(-8, 9), 1, 100);
    }

    /// <summary>
    /// Shuffles the pool and queues as many faces as there are players to be created.
    /// </summary>
    private static Queue<string> DealFaces(int playerCount, Random random)
    {
        var pool = FaceCatalog.All.ToList();

        for (var index = pool.Count - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (pool[index], pool[swap]) = (pool[swap], pool[index]);
        }

        while (pool.Count < playerCount)
        {
            pool.AddRange(FaceCatalog.All);
        }

        return new Queue<string>(pool.Take(playerCount));
    }

    /// <summary>
    /// Gives a face to every player who does not have one yet.
    /// </summary>
    private async Task GiveFacesToTheWorldAlreadySeededAsync(
        Random random,
        CancellationToken cancellationToken)
    {
        var faceless = await _dbContext.Players
            .Where(player => player.Face == null)
            .ToListAsync(cancellationToken);

        if (faceless.Count == 0)
        {
            return;
        }

        var faces = DealFaces(faceless.Count, random);

        foreach (var player in faceless)
        {
            player.SetFace(faces.Dequeue());
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Gave a face to {Count} players that had none.", faceless.Count);
    }

    /// <summary>
    /// Gives a manager to every team that does not have one yet.
    /// </summary>
    private async Task GiveManagersToTheWorldAlreadySeededAsync(
        Random random,
        CancellationToken cancellationToken)
    {
        var teamsWithoutManager = await _dbContext.Teams
            .Where(team => !_dbContext.Managers.Any(m => m.TeamId == team.Id))
            .ToListAsync(cancellationToken);

        if (teamsWithoutManager.Count == 0)
        {
            return;
        }

        var coachPool = CoachNames.OrderBy(_ => random.Next()).ToList();

        for (var index = 0; index < teamsWithoutManager.Count; index++)
        {
            var team = teamsWithoutManager[index];
            var coachName = coachPool[index % coachPool.Count];
            _dbContext.Managers.Add(Manager.Create(team.Id, coachName));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Gave a manager to {Count} teams that had none.", teamsWithoutManager.Count);
    }

    /// <summary>
    /// The books of a world that was seeded before clubs had one.
    /// </summary>
    private async Task OpenTheBooksOfTheWorldAlreadySeededAsync(CancellationToken cancellationToken)
    {
        var season = await _dbContext.Seasons
            .OrderBy(candidate => candidate.Number)
            .FirstOrDefaultAsync(cancellationToken);

        if (season is null)
        {
            return;
        }

        var withoutABook = await _dbContext.Teams
            .Where(team => !_dbContext.FinanceMovements.Any(movement => movement.TeamId == team.Id))
            .ToListAsync(cancellationToken);

        if (withoutABook.Count == 0)
        {
            return;
        }

        _dbContext.FinanceMovements.AddRange(withoutABook.Select(
            team => FinanceMovement.Seed(team.Id, season.Id, FinanceRules.StartingBalance)));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Opened the books of {Count} clubs with {Capital} limos each.",
            withoutABook.Count,
            FinanceRules.StartingBalance);
    }

    /// <summary>
    /// A squad needs a couple of goalkeepers, not only the one that starts, and the rest
    /// of the slots are spread over DEF, MID and ATT as evenly as possible.
    /// </summary>
    private IEnumerable<Position> BuildPositions(int playersPerTeam, int goalkeepers)
    {
        if (playersPerTeam < 1)
        {
            throw new InvalidOperationException("A squad needs at least one player.");
        }

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

    private const int seedSalt = 7717;

    /// <summary>
    /// Deals the season's intake: the young free agents a season opens with, unattached and
    /// waiting to be signed.
    /// </summary>
    public async Task GenerateYoungPlayersAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (season is null)
        {
            _logger.LogWarning("No season found for {SeasonId}; young players were not generated.", seasonId);
            return;
        }

        var random = _options.RandomSeed.HasValue
            ? new Random(_options.RandomSeed.Value + season.Number)
            : Random.Shared;

        var created = await AddYoungFreeAgentsAsync(season.Id, YouthIntakeRules.FreeAgentsPerSeason, random, cancellationToken);

        _logger.LogInformation(
            "Dealt the season {SeasonNumber} intake: {Count} young free agents.",
            season.Number, created);
    }

    /// <summary>
    /// Fills the market with a given number of young free agents, aged sixteen to nineteen.
    /// </summary>
    public async Task<int> SeedYoungFreeAgentsAsync(
        int count = YouthIntakeRules.FreeAgentsPerSeason,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        var season = seasonId is { } wanted
            ? await _dbContext.Seasons.FirstOrDefaultAsync(s => s.Id == wanted, cancellationToken)
            : await _dbContext.Seasons
                .Where(s => s.Status == Domain.Enums.SeasonStatus.InProgress)
                .OrderByDescending(s => s.Number)
                .FirstOrDefaultAsync(cancellationToken);

        if (season is null)
        {
            _logger.LogWarning("There is no season in progress; the young free agents were not seeded.");
            return 0;
        }

        var random = _options.RandomSeed.HasValue
            ? new Random(_options.RandomSeed.Value + seedSalt)
            : Random.Shared;

        var created = await AddYoungFreeAgentsAsync(season.Id, count, random, cancellationToken);

        _logger.LogInformation(
            "Seeded {Count} young free agents into season {SeasonNumber}.", created, season.Number);

        return created;
    }

    /// <summary>
    /// The men themselves: a birth date drawn so the player is between
    /// <see cref="YouthIntakeRules.YoungestAge"/> and <see cref="YouthIntakeRules.OldestAge"/>
    /// on the day the season opens, each built by the seeded world's own formulas, each dealt a
    /// face from the pool, each attached to a season with no club and no contract.
    /// </summary>
    private async Task<int> AddYoungFreeAgentsAsync(
        Guid seasonId,
        int count,
        Random random,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        var faces = DealFaces(count, random);
        var players = new List<Player>();
        var states = new List<PlayerSeasonState>();

        for (var i = 0; i < count; i++)
        {
            var age = random.Next(
                YouthIntakeRules.YoungestAge,
                YouthIntakeRules.OldestAge + 1);

            var isGoalkeeper = random.NextDouble() < YouthIntakeRules.GoalkeeperShare;
            var position = isGoalkeeper ? Position.GK : (Position)random.Next(1, 4);

            var player = CreatePlayer(position, random, faces.Dequeue(), age);

            players.Add(player);
            states.Add(PlayerSeasonState.CreateFreeAgent(
                player.Id,
                seasonId,
                random.Next(_options.MinimumEnergy, _options.MaximumEnergy + 1)));
        }

        _dbContext.Players.AddRange(players);
        _dbContext.PlayerSeasonStates.AddRange(states);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return players.Count;
    }
}