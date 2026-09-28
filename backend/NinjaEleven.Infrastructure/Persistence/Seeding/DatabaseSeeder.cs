using NinjaEleven.Application.Abstractions;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Managers;
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
/// Creates the starting world: name pools, one season, one competition with all teams
/// enrolled, and a randomly generated squad per team. It never overwrites an existing
/// world: seeding is skipped as soon as a team is already present.
/// </summary>
public class DatabaseSeeder : IDataSeeder
{
    /// <summary>
    /// The starting world: thirty-six clubs, twelve to a division, strongest first.
    ///
    /// The catalog is a rule rather than a setting because the pyramid is one. A pyramid of
    /// three divisions of twelve is what promotion and relegation move clubs between, so a
    /// world seeded with any other number would be a pyramid the season's own rules cannot
    /// describe. The order of the catalog is the order of the divisions: the first twelve
    /// clubs are the top flight, the next twelve the second, and the last twelve the third.
    ///
    /// Strength is seeded per tier and not per club, so a division means something before a
    /// ball is kicked. Without that, a third-division club would be as good as a first-
    /// division one, the table would be noise from the first matchday, and a manager would
    /// have no reason to want promotion rather than to stay where he is.
    /// </summary>
    private static readonly (string Name, string ShortName, string Primary, string Secondary, int Tier)[] TeamCatalog =
    {
        // 1ª Divisão
        ("Rio Branco Esporte Clube", "RBE", "#B11226", "#F5F5F5", 1),
        ("Ferroviário Atlético", "FER", "#1B3A6B", "#D4AF37", 1),
        ("Estrela do Norte", "EDN", "#0F5132", "#FFD700", 1),
        ("União Serrana", "UNS", "#7B2D8B", "#F0F0F0", 1),
        ("Porto Marítimo", "PTM", "#00693E", "#1F1F1F", 1),
        ("Clube Aurora", "CAU", "#E07B00", "#2B2B2B", 1),
        ("Real Serrano", "RSE", "#0D6EFD", "#FFFFFF", 1),
        ("Vila Nova do Vale", "VNV", "#8B0000", "#D9D9D9", 1),
        ("Atlético do Planalto", "ADP", "#004B8D", "#FFD700", 1),
        ("Grêmio Litorâneo", "GLI", "#0B6623", "#F5F5F5", 1),
        ("Esporte Clube Serra Azul", "SAZ", "#5C2D91", "#FFFFFF", 1),
        ("Rio Pardo Futebol Clube", "RPF", "#C8102E", "#000000", 1),

        // 2ª Divisão
        ("Nacional Serranense", "NSE", "#1E6F5C", "#F0E68C", 2),
        ("Clube Atlético Barreiras", "CAB", "#0F4C81", "#FFD700", 2),
        ("Esporte Clube Laranjeiras", "ECL", "#E85A0C", "#2B2B2B", 2),
        ("Grêmio Ferroviário do Sul", "GFS", "#37474F", "#E53935", 2),
        ("Atlético Bandeirante", "ATB", "#2E7D32", "#FFFFFF", 2),
        ("Sociedade Esportiva Cerradão", "SEC", "#6A1B9A", "#F5F5F5", 2),
        ("União Atlético Maravilha", "UAM", "#00838F", "#FFEB3B", 2),
        ("Clube Náutico Ipanema", "CNI", "#0277BD", "#FFFFFF", 2),
        ("Esporte Clube Palmeiral", "EPL", "#EF6C00", "#1B5E20", 2),
        ("Grêmio Esportivo Andorinha", "GEA", "#455A64", "#FFCA28", 2),
        ("Clube Atlético Santa Clara", "CSC", "#7B1FA2", "#F5F5F5", 2),
        ("Sport Club Interface", "SCI", "#212121", "#00E5FF", 2),

        // 3ª Divisão
        ("Associação Atlética Guarani", "AAG", "#1565C0", "#FFFFFF", 3),
        ("Clube Esportivo Tijuco", "CET", "#2E7D32", "#212121", 3),
        ("Grêmio Operário Seridoense", "GOS", "#4527A0", "#FFD700", 3),
        ("Esporte Clube Riachuelo", "ECR", "#AD1457", "#F5F5F5", 3),
        ("Sociedade Recreativa Estância", "SRE", "#00695C", "#FF8F00", 3),
        ("Clube Atlético Juazeirense", "CAJ", "#283593", "#FFFFFF", 3),
        ("Grêmio Esportivo Várzea Nova", "GVN", "#33691E", "#F5F5F5", 3),
        ("Sport Club Aurora Sul", "SAS", "#5D4037", "#FFD54F", 3),
        ("Associação Esportiva Cristal", "AEC", "#00838F", "#263238", 3),
        ("Clube Esportivo Umbuzeiro", "CEU", "#9E9D24", "#FFFFFF", 3),
        ("Grêmio Atlético Potiguar", "GAP", "#C62828", "#FFFFFF", 3),
        ("Esporte Clube Dourado", "ECD", "#F9A825", "#212121", 3)
    };

    /// <summary>
    /// A pool of coach names: one per club in the catalog so every team gets a distinct
    /// manager when the world is seeded. The names are Portuguese-sounding and varied.
    /// </summary>
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
        "Márcio Fernandes"
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
        ("Tech Solutions", "Tecnologia", "#00B0FF")
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
        await SeedSponsorsAsync(cancellationToken);

        var random = _options.RandomSeed.HasValue
            ? new Random(_options.RandomSeed.Value)
            : Random.Shared;

        if (await _dbContext.Teams.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Seeding skipped: the world already contains teams.");
            await GiveFacesToTheWorldAlreadySeededAsync(random, cancellationToken);
            await GiveManagersToTheWorldAlreadySeededAsync(random, cancellationToken);
            await OpenTheBooksOfTheWorldAlreadySeededAsync(cancellationToken);
            return;
        }

        await SeedStartingWorldAsync(random, cancellationToken);
        await GiveFacesToTheWorldAlreadySeededAsync(random, cancellationToken);
        await GiveManagersToTheWorldAlreadySeededAsync(random, cancellationToken);
        await OpenTheBooksOfTheWorldAlreadySeededAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the starting world for a brand-new database: one season, three divisions, a
    /// cup and a Supercup, thirty-six clubs with squads, and books opened for each.
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

        var participants = new List<CompetitionParticipant>();
        var teams = new List<Team>();
        var stadiums = new List<Stadium>();
        var players = new List<Player>();
        var seasonStates = new List<PlayerSeasonState>();
        var memberships = new List<TeamMembership>();
        var movements = new List<FinanceMovement>();
        var managers = new List<Manager>();
        var faces = DealFaces(TeamCatalog.Length * _options.PlayersPerTeam, random);

        var coachPool = CoachNames.OrderBy(_ => random.Next()).ToList();

        // One coach per club, and a coach is a name a club is told apart by. A pool shorter
        // than the catalog is not something to paper over by handing two clubs the same man:
        // it means a club in the catalog has nobody to be managed by, and the failure belongs
        // where somebody can read it and add a name, not as an index out of range forty lines
        // later in a loop over teams.
        if (coachPool.Count < TeamCatalog.Length)
        {
            throw new InvalidOperationException(
                $"The coach pool has {coachPool.Count} names for {TeamCatalog.Length} clubs. " +
                "Every club needs its own manager, so the pool has to cover the catalog.");
        }

        for (var index = 0; index < TeamCatalog.Length; index++)
        {
            var definition = TeamCatalog[index];
            var team = CreateTeam(definition, random);
            var stadium = Stadium.Create(team.Id, definition.Name);

            teams.Add(team);
            stadiums.Add(stadium);
            team.SetStadium(stadium);

            participants.Add(CompetitionParticipant.Create(
                leagueEditions[definition.Tier - 1].Id,
                team.Id));

            movements.Add(FinanceMovement.Seed(team.Id, season.Id, FinanceRules.StartingBalance));

            var coachName = coachPool[index];
            managers.Add(Manager.Create(team.Id, coachName));

            var squad = CreateSquad(team, season.Id, season.Number, startDate, random, faces);

            players.AddRange(squad.Players);
            seasonStates.AddRange(squad.SeasonStates);
            memberships.AddRange(squad.Memberships);
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
        // free agents a club signs rather than buys. They are dealt after the world is saved
        // because the intake reads the season it is dealt into, and a world written with every
        // one of its three hundred names under contract is a world whose market has nothing in
        // it for a manager to sign.
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
    /// The first season of a new world.
    ///
    /// It is numbered one rather than named for a year, because the number is the season's
    /// identity: a world that started at "Temporada IX" because it happened to begin in 2026
    /// would have to be renumbered the day somebody played nine seasons.
    /// </summary>
    private static Season CreateSeason()
    {
        var season = Season.Create(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        season.Start();
        return season;
    }

    /// <summary>
    /// A club, with a reputation drawn from the band its division occupies.
    ///
    /// The bands overlap on purpose. A top-division club and a second-division club are two
    /// draws from adjacent ranges, so a strong second-division side can still be a promotion
    /// candidate, which is what makes a table worth managing rather than a foregone conclusion
    /// written down before kick-off.
    /// </summary>
    private static Team CreateTeam(
        (string Name, string ShortName, string Primary, string Secondary, int Tier) definition,
        Random random)
    {
        return Team.Create(
            definition.Name,
            definition.ShortName,
            definition.Primary,
            definition.Secondary);
    }

    private static (int Low, int High) ReputationBand(int tier) => tier switch
    {
        1 => (62, 90),
        2 => (52, 78),
        _ => (42, 68)
    };

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
    /// A player with a birth date somebody else has already decided — the young men the market
    /// is fed with, who are sixteen to nineteen by rule rather than by the draw.
    ///
    /// Everything else is the seeded world's own formula, and that is the point: a prospect
    /// arriving from the base is built by the same expression that builds a squad, so his keeper
    /// is a keeper and his outfield men are outfield men. A generator that gave a seventeen-year-
    /// old goalkeeper the same band as a centre back would be filling the market with players no
    /// engine would ever pick, and a market full of players nobody can field is a market that
    /// looks busy and is empty.
    /// </summary>
    private Player CreatePlayer(Position position, Random random, string face, int age)
    {
        var firstName = NameCatalog.AllFirstNames[random.Next(NameCatalog.AllFirstNames.Count)];
        var surname = NameCatalog.AllSurnames[random.Next(NameCatalog.AllSurnames.Count)];

        // A goalkeeper is built for his job: the engine weighs reflexes and power when it
        // picks the eleven, so a keeper with outfield attributes would never be chosen.
        var isGoalkeeper = position == Position.GK;

        return Player.Create(
            $"{firstName} {surname}",
            age,
            position,
            speed: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            accuracy: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            dribbling: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            heading: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            strength: isGoalkeeper ? RandomAttribute(random) : OutfieldAttribute(random),
            goalkeeperPower: isGoalkeeper ? StrongAttribute(random) : 0,
            reflexes: isGoalkeeper ? StrongAttribute(random) : 0,
            face: face);
    }

    /// <summary>
    /// Shuffles the pool and queues as many faces as there are players to be created. It is
    /// dealt from the front rather than drawn per player because a club of twenty-three men
    /// with two men sharing a face reads as a copy-paste, and a shuffled queue cannot repeat
    /// one until the pool is exhausted.
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
    ///
    /// The pool of faces arrived after the world was already seeded, and the seeder skips a
    /// world that has teams in it, so nobody would have a face. Resuming seeding is also the
    /// honest answer to a player added later by a future feature: the identity is missing
    /// something, and this is the place that fills it in.
    /// </summary>
    private async Task GiveFacesToTheWorldAlreadySeededAsync(
        Random random,
        CancellationToken cancellationToken)
    {
        // Only null counts. The column is jsonb, and `''` is not a JSON document, so there is
        // no second way for a face to be missing — and asking Postgres whether a jsonb
        // equals an empty string is an error, not a question it will answer.
        var faceless = await _dbContext.Players
            .Where(player => player.Face == null)
            .ToListAsync(cancellationToken);

        if (faceless.Count == 0)
        {
            return;
        }

        // Dealt and not drawn, for the same reason a new world is dealt: a squad in which
        // two men share a face reads as a copy-paste, and a backfill has the same problem a
        // fresh seeding has.
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
    ///
    /// Managers were added after the world was already seeded, so teams created before that
    /// have no manager. This backfills them with names from the coach pool.
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
    ///
    /// Every club without a single line in its book is given the capital a new club is
    /// founded with. A world that predates the money is not given a backdated history of gate
    /// receipts and wage bills invented for it: the honest opening line of a book whose
    /// earlier pages nobody kept is the capital, and everything after it is a movement the
    /// game actually made.
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

    /// <summary>
    /// Mixed into the backfill's seed so a backfill does not reproduce the exact same squad of
    /// eighteen-year-olds the last one dealt.
    /// </summary>
    private const int seedSalt = 7717;

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

    /// <summary>
    /// Deals the season's intake: the young free agents a season opens with, unattached and
    /// waiting to be signed.
    ///
    /// The count is <see cref="YouthIntakeRules.FreeAgentsPerSeason"/> and it does not depend on
    /// what happened in the season before, because the rule is a count and not a ratio: a
    /// season's first day has no retirements to divide by, and a market fed by a ratio opens
    /// its first season with nobody in it at all — three hundred names who all belong to
    /// somebody, and not one signing a manager could make. Every season gets its intake, the
    /// same size, so the market a club shops in is never empty of a player it could pick up.
    ///
    /// It is called once per season, when the season opens: by the world being written for the
    /// first one and by the season close for every one after. It is not called again on a
    /// restart, so a world never grows a second intake into the same season.
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
    ///
    /// This is the backfill the rules ask for — a market with nothing in it is a market a manager
    /// opens, sees three hundred under contract men and no way to improve any of them, and leaves
    /// — and it is deliberately a count rather than a ratio: a ratio needs retirements to divide
    /// and a world being seeded has none. The men are drawn from the same pool the squads are
    /// drawn from, dealt faces like everybody else, and given no club: a young player is somebody
    /// a club signs, not somebody a club buys.
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
    ///
    /// The age is drawn straight from the rule's band, because the age is now the fact the
    /// world keeps about a player: there is no date to fall out of step with it, and the boy
    /// who arrives in December and the boy who arrives in January are the age they are drawn
    /// as on every screen of the game.
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
