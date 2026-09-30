using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Api.Contracts;

public class PlayerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; }
    public Position Position { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Stamina { get; init; }
    public int Potential { get; init; }
    public double Stars { get; init; }
}

public class PlayerSeasonStateDto
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public Guid SeasonId { get; init; }
    public Guid? TeamId { get; init; }
    public int Energy { get; init; }
    public int Goals { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int SuspensionMatches { get; init; }
    public Injury Injury { get; init; }

    /// <summary>
    /// Matches of his club the injury still keeps him out of, so a screen can say for how
    /// long instead of only that something is wrong.
    /// </summary>
    public int InjuryMatchesRemaining { get; init; }

    public bool IsAvailable { get; init; }
}

public class SquadPlayerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; }
    public Position Position { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Stamina { get; init; }
    public int Potential { get; init; }
    public double Stars { get; init; }

    public int Energy { get; init; }
    public int Goals { get; init; }

    /// <summary>
    /// Saves for the club this season. A keeper's answer to "how has he done", and the
    /// same number the player profile shows, drawn from the same season state rather than
    /// reassembled a second way.
    /// </summary>
    public int Saves { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int SuspensionMatches { get; init; }
    public Injury Injury { get; init; }

    /// <summary>
    /// Matches of his club the injury still keeps him out of, so a screen can say for how
    /// long instead of only that something is wrong.
    /// </summary>
    public int InjuryMatchesRemaining { get; init; }

    /// <summary>How many times he has been hurt this season, which is not the same as the
    /// injury he is carrying: a player who came back from a knock has none and has still
    /// been knocked. The market reads this one.</summary>
    public int Injuries { get; init; }

    /// <summary>What he is worth, in limos, and what the club owes for his contract.</summary>
    public decimal MarketValue { get; init; }
    public decimal Salary { get; init; }
    public int ContractSeasons { get; init; }

    /// <summary>
    /// How many seasons of the contract are left, one being the last, and what another club
    /// would have to pay for him. The asking price equals his value in the last season of a
    /// contract and is a fifth more than it while he is not, so a manager reading a transfer
    /// list sees the price he would have to agree rather than the price the player is worth.
    /// </summary>
    public int SeasonsLeft { get; init; }
    public bool IsInLastSeason { get; init; }
    public decimal AskingPrice { get; init; }

    public bool IsAvailable { get; init; }
    public bool Retiring { get; init; }
    public Guid? TeamId { get; init; }
    public Guid SeasonId { get; init; }
}

/// <summary>
/// A player whole, for the profile screen: who he is, the two totals columns, and every
/// match he played. The screen filters the list by season; it does not refetch, because
/// then the two filters would be two different sets of matches.
/// </summary>
public class PlayerProfileDto
{
    public Guid PlayerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public int Age { get; init; }
    public int Speed { get; init; }
    public int Accuracy { get; init; }
    public int Dribbling { get; init; }
    public int Heading { get; init; }
    public int Strength { get; init; }
    public int GoalkeeperPower { get; init; }
    public int Reflexes { get; init; }
    public int Stamina { get; init; }
    public int Potential { get; init; }
    public double Stars { get; init; }
    public Guid? SeasonId { get; init; }

    /// <summary>
    /// Each attribute read as stars, beside the attribute itself, because a card shows both
    /// and the star is the same conversion the engine would make. They are worked out by
    /// <see cref="Domain.Players.PlayerRating.AttributeToStars"/> on the way out and never by
    /// the client: the card used to divide the raw value by two, which was right while the
    /// attributes ran 1..20 and has been the wrong answer on every card since they moved to
    /// 1..100 — a 34 was drawn as five stars and a 90 as five.
    /// </summary>
    public double SpeedStars { get; init; }
    public double AccuracyStars { get; init; }
    public double DribblingStars { get; init; }
    public double HeadingStars { get; init; }
    public double StrengthStars { get; init; }
    public double GoalkeeperPowerStars { get; init; }
    public double ReflexesStars { get; init; }
    public Guid? TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string? TeamPrimaryColor { get; init; }
    public string? TeamSecondaryColor { get; init; }
    public int Energy { get; init; }
    public bool IsAvailable { get; init; }
    public string Injury { get; init; } = string.Empty;
    public int InjuryMatchesRemaining { get; init; }

    /// <summary>
    /// What he is worth, and what the club owes for his contract, in limos. The wage is a
    /// share of the price, so a manager can see the two and know which players are costing
    /// the club more than they are worth.
    /// </summary>
    public decimal MarketValue { get; init; }
    public decimal Salary { get; init; }
    public int ContractSeasons { get; init; }

    /// <summary>
    /// How many seasons of the contract are left, one being the last, and what another club
    /// would have to pay for him. The asking price is his own value in the last season of a
    /// contract and a fifth more on top of it while he is not, so a card shows the price a
    /// rival would have to agree rather than only what the player is worth.
    /// </summary>
    public int SeasonsLeft { get; init; }
    public bool IsInLastSeason { get; init; }
    public decimal AskingPrice { get; init; }

    /// <summary>
    /// The player's face as the raw JSON of a faces.js <c>FaceConfig</c>, null when he has
    /// none. It travels as a string rather than as a typed object so the contract is the
    /// library's own: the client draws it with faces.js, and neither side holds a copy of the
    /// face shape to drift.
    /// </summary>
    public string? Face { get; init; }

    public PlayerCareerLineDto Season { get; init; } = new();
    public PlayerCareerLineDto Total { get; init; } = new();
    public List<PlayerMatchLineDto> History { get; init; } = new();
}

/// <summary>
/// One row of the two totals columns. Appearances is a pair, not a number: "14 (3) [2]" is
/// fourteen matches, three of them off the bench, and two on the bench unused.
/// </summary>
public class PlayerCareerLineDto
{
    public int Appearances { get; init; }
    public int Started { get; init; }
    public int CameOn { get; init; }
    public int BenchUnused { get; init; }
    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Saves { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int Injuries { get; init; }
    public int MatchesMissed { get; init; }
}

public class PlayerMatchLineDto
{
    public Guid MatchId { get; init; }
    public Guid? SeasonId { get; init; }
    public bool Started { get; init; }
    public bool CameOn { get; init; }
    public bool SubbedOff { get; init; }
    public bool WasOnBenchUnused { get; init; }
    public int Goals { get; init; }
    public int OwnGoals { get; init; }
    public int Saves { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public bool WasInjured { get; init; }
    public bool InjuredOff { get; init; }
    public bool IsHome { get; init; }
    public string OpponentName { get; init; } = string.Empty;
    public Guid? OpponentTeamId { get; init; }
    public string? OpponentTeamPrimaryColor { get; init; }
    public string? OpponentTeamSecondaryColor { get; init; }
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public int RoundNumber { get; init; }

    /// <summary>
    /// The club he played for, and the match read as a fixture: the season, the competition,
    /// the round or phase, the ground and the crowd. These are the same words the club page's
    /// match table is written in, because it is the same table — a manager reading a striker's
    /// history and a manager reading his club's last matches are reading the same matches.
    /// </summary>
    public string? TeamName { get; init; }
    public string? SeasonName { get; init; }
    public string? CompetitionName { get; init; }
    public string? PhaseName { get; init; }
    public string? StadiumName { get; init; }
    public int? Attendance { get; init; }
}
