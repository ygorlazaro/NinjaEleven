namespace NinjaEleven.Application.Models;

/// <summary>
/// A player as a manager reads him: who he is, what he is worth this season, and what he
/// has actually done in the matches he has played.
/// </summary>
public class PlayerProfile
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int Age { get; set; }
    public int Speed { get; set; }
    public int Accuracy { get; set; }
    public int Dribbling { get; set; }
    public int Heading { get; set; }
    public int Strength { get; set; }
    public int GoalkeeperPower { get; set; }
    public int Reflexes { get; set; }

    /// <summary>
    /// The player's face as the raw JSON of a faces.js <c>FaceConfig</c>, or null when he has
    /// none. It is carried as a string on purpose: the shape belongs to the library that draws
    /// it, and a copy of that shape in C# would be a second one to keep in step.
    /// </summary>
    public string? Face { get; set; }

    /// <summary>Season, energy and availability, when the player belongs to one.</summary>
    public Guid? SeasonId { get; set; }

    public Guid? TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;
    public string? TeamPrimaryColor { get; set; }
    public string? TeamSecondaryColor { get; set; }

    public int Energy { get; set; }
    public bool IsAvailable { get; set; }
    public string Injury { get; set; } = string.Empty;
    public int InjuryMatchesRemaining { get; set; }

    /// <summary>
    /// What he is worth on the market and what the club owes for his contract, in limos.
    ///
    /// They are read off the same season state the market reads, so the price on a profile
    /// and the wage on the club's wage bill are one number and its share rather than two
    /// screens that each have their own idea of what a player is.
    /// </summary>
    public decimal MarketValue { get; set; }
    public decimal Salary { get; set; }
    public int ContractSeasons { get; set; } = NinjaEleven.Domain.Finance.FinanceRules.DefaultContractSeasons;

    /// <summary>
    /// How much of the contract is left, and what it would cost another club to break it.
    ///
    /// Seasons left is what a manager negotiates with: a man in the last year of his deal can
    /// be signed without paying a club two more seasons of wages, and a man with two years
    /// to run costs a fifth more than he is worth. The two numbers are kept apart on the card
    /// for the same reason they are kept apart in the domain — one is what the player is
    /// worth and the other is what taking him costs, and a manager who is shown only the
    /// first will offer the second.
    /// </summary>
    public int SeasonsLeft { get; set; }
    public bool IsInLastSeason { get; set; }
    public decimal AskingPrice { get; set; }

    /// <summary>The season's line, and the career's, so a table can show both.</summary>
    public PlayerCareerLine Season { get; set; } = new();

    public PlayerCareerLine Total { get; set; } = new();

    /// <summary>
    /// Every match he has a line for, newest first. The whole career comes back and the
    /// screen filters it, because a season filter that refetched on every click would be a
    /// second source of truth about which matches count.
    /// </summary>
    public List<PlayerMatchLine> History { get; set; } = new();
}

/// <summary>
/// What a player has done, summed over a set of matches. Appearances keep the two things a
/// single number loses apart: games started and games entered off the bench, which is what
/// "14 (3) [2]" is made of.
/// </summary>
public class PlayerCareerLine
{
    public int Appearances { get; set; }
    public int Started { get; set; }
    public int CameOn { get; set; }
    public int BenchUnused { get; set; }
    public int Goals { get; set; }
    public int OwnGoals { get; set; }
    public int Saves { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Injuries { get; set; }

    /// <summary>Matches off the pitch because he was carrying a knock.</summary>
    public int MatchesMissed { get; set; }
}

/// <summary>
/// One club's share of a player's career: what he did in the shirt, and over how many seasons.
///
/// A career total on its own is half an answer. Six goals in five years says a man moved; the
/// breakdown says where he scored them, and a market that shows only the sum is asking a club
/// to pay for a whole career while saying nothing about which part of it this club is buying.
/// The line is grouped by the club on the match itself, not by the club he finished at: a goal
/// is scored in a shirt, and the shirt is what the number belongs to.
/// </summary>
public class PlayerClubCareerLine
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    /// <summary>Seasons in which the player has a line for this club, across the whole career.</summary>
    public int Seasons { get; set; }

    public PlayerCareerLine Total { get; set; } = new();
}

/// <summary>
/// One player's whole career, keyed by the player, so a market listing can show what a man has
/// done without asking the database about him one at a time.
/// </summary>
public class CareerTotals
{
    public Guid PlayerId { get; set; }

    public PlayerCareerLine Line { get; set; } = new();
}

/// <summary>
/// One match in a player's history: who he played against, what the score was, and what he
/// did. The match is named so a manager can go and watch it, which is the only reason a
/// history of games is worth keeping.
/// </summary>
public class PlayerMatchLine
{
    public Guid MatchId { get; set; }
    public Guid? SeasonId { get; set; }
    public bool Started { get; set; }
    public bool CameOn { get; set; }
    public bool SubbedOff { get; set; }
    public bool WasOnBenchUnused { get; set; }
    public int Goals { get; set; }
    public int OwnGoals { get; set; }
    public int Saves { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public bool WasInjured { get; set; }
    public bool InjuredOff { get; set; }

    public bool IsHome { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public Guid? OpponentTeamId { get; set; }
    public string? OpponentTeamPrimaryColor { get; set; }
    public string? OpponentTeamSecondaryColor { get; set; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public int RoundNumber { get; set; }

    /// <summary>
    /// The club he was playing for. A career crosses clubs, so a line that named only the
    /// opponent leaves the other half of the fixture unsaid, and "he scored twice" becomes a
    /// sentence about nobody in particular.
    /// </summary>
    public string? TeamName { get; set; }

    /// <summary>
    /// The match read the way the club page reads one: which season and competition it belonged
    /// to, the round or the phase of it, the ground and the crowd. A player's history is the
    /// same table of fixtures read by a man rather than by a club, and it is the same query
    /// for the same reason — two tables that answered the same question differently would be
    /// two accounts of the same afternoon.
    /// </summary>
    public string? SeasonName { get; set; }
    public string? CompetitionName { get; set; }
    public string? PhaseName { get; set; }
    public string? StadiumName { get; set; }
    public int? Attendance { get; set; }
}

/// <summary>
///     A player's line in a match, with the match itself already resolved.
    ///
    /// The history is read in one query rather than a query per row: a striker with a hundred
    /// appearances would otherwise cost a hundred round trips to draw a table, and a screen
    /// that took that long to open would be a screen nobody opens.
    /// </summary>
    public class PlayerMatchRecord
    {
        public Guid MatchId { get; set; }
        public Guid? SeasonId { get; set; }
        public bool Started { get; set; }
        public bool CameOn { get; set; }
        public bool SubbedOff { get; set; }
        public bool WasOnBenchUnused { get; set; }
        public int Goals { get; set; }
        public int OwnGoals { get; set; }
        public int Saves { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public bool WasInjured { get; set; }
        public bool InjuredOff { get; set; }
        public bool IsHome { get; set; }
        public string OpponentName { get; set; } = string.Empty;
        public Guid? OpponentTeamId { get; set; }
        public string? OpponentTeamPrimaryColor { get; set; }
        public string? OpponentTeamSecondaryColor { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public int RoundNumber { get; set; }
        public string? TeamName { get; set; }
        public string? SeasonName { get; set; }
        public string? CompetitionName { get; set; }
        public string? PhaseName { get; set; }
        public string? StadiumName { get; set; }
        public int? Attendance { get; set; }
    }
