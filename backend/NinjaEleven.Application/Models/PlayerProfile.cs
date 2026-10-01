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

    /// <summary>How much he has in the tank, on the same 1..100 scale as the rest.</summary>
    public int Stamina { get; set; }

    /// <summary>
    /// The ceiling on his reading, on the same 1..100 scale. It is on the profile because a
    /// profile is where a manager asks what a man will become, and a screen that can only
    /// show what a man is has no way to answer it.
    /// </summary>
    public int Potential { get; set; }

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
    /// The number he wears for the club on this card, or null when he is on nobody's books.
    ///
    /// It travels with the contract rather than with the player, so the same man shown on
    /// two cards for two clubs is wearing two numbers — and a profile of a free agent says
    /// nothing at all rather than repeating the last shirt he happened to wear.
    /// </summary>
    public int? ShirtNumber { get; set; }

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

/// <summary>
/// One attribute of a training quote: what it is at, and what a session on it would cost.
///
/// <para>
/// It is a pair rather than a number because the manager is being offered a decision, and a
/// decision needs both halves. An attribute whose price is shown without its value tells him
/// nothing about whether the session is worth taking, and a value shown without its price
/// makes the choice a guess.
/// </para>
/// </summary>
public class TrainingAttributeQuote
{
    public string Attribute { get; set; } = string.Empty;
    public int Value { get; set; }

    /// <summary>
    /// What one session on this attribute costs, in energy, or null when a session is
    /// impossible — a goalkeeper attribute on an outfielder, or one already at the man's
    /// ceiling. Null is what lets a screen offer nothing instead of offering a button the
    /// backend will refuse, and the reason a client never has to know the rule that made it
    /// null.
    /// </summary>
    public int? Cost { get; set; }
}

/// <summary>
/// A player's whole training sheet: who he is, what he has to spend, and what each of the
/// eight would cost him.
///
/// <para>
/// A squad's sheets are one read rather than one read per player. A manager opening the
/// training screen is asking a question about twenty-three men, and asking it a man at a time
/// would be twenty-three round trips to answer a question the backend can answer in one — and
/// the last answer would arrive after the first had already been clicked, so the prices on
/// screen would be from two different moments.
/// </para>
/// </summary>
public class TrainingQuote
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int Age { get; set; }
    public int Potential { get; set; }
    public int Stamina { get; set; }
    public int Energy { get; set; }
    public bool IsAvailable { get; set; }
    public string Injury { get; set; } = "None";

    /// <summary>
    /// What one session on this man costs the club, being a share of his season wage.
    /// </summary>
    /// <remarks>
    /// It is on every man's sheet rather than once on the squad's because it is a per-man
    /// number: the same session costs the club a great deal more of a striker's wage than of a
    /// reserve goalkeeper's, and a single figure on the header would price a decision that is
    /// not one decision but twenty-three. It is a wage and not a share of one, so the manager
    /// can check it against the salary he already reads on the same screen.
    /// </remarks>
    public decimal SessionFee { get; set; }

    public List<TrainingAttributeQuote> Attributes { get; set; } = new();
}

/// <summary>
/// The club's whole training sheet, with what the squad has left between them.
///
/// <para>
/// The total is here rather than added up on the screen because it is the number a manager
/// plans a week with, and a total the client assembled from twenty-three rows it had already
/// rounded would be a total nobody could reproduce.
/// </para>
/// </summary>
public class SquadTrainingQuotes
{
    public Guid TeamId { get; set; }
    public Guid SeasonId { get; set; }
    public int SquadEnergy { get; set; }

    /// <summary>
    /// The calendar day the allowance on this sheet is for. It is named rather than implied by
    /// the client, because the allowance is a day's allowance and a screen that showed a
    /// number without saying which day it belonged to would be showing yesterday's budget at
    /// tomorrow's prices after midnight.
    /// </summary>
    public DateOnly Day { get; set; }

    /// <summary>
    /// Whether the club has a fixture on <see cref="Day"/>, which is the whole difference
    /// between one session and two.
    /// </summary>
    public bool PlaysToday { get; set; }

    /// <summary>How many sessions the club has on <see cref="Day"/>.</summary>
    public int SessionsAllowed { get; set; }

    /// <summary>How many of them have been spent.</summary>
    public int SessionsSpent { get; set; }

    public List<TrainingQuote> Players { get; set; } = new();
}
