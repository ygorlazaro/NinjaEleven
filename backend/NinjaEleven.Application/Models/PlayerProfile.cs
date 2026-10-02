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
    /// Whether the player has announced retirement at the end of this season.
    /// Read from the season state so the profile and the squad table agree.
    /// </summary>
    public bool Retiring { get; set; }

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
    /// <para>
    /// The salary is the figure on the contract, so it is the same number on the card, on the
    /// squad table and on the club's wage bill — a wage worked out from his attributes today
    /// would be a different number in each of those places, and a manager budgeting a season
    /// would be budgeting one of them.
    /// </para>
    /// </summary>
    public decimal MarketValue { get; set; }
    public decimal Salary { get; set; }

    /// <summary>
    /// What the club would pay him for a season if it signed him again today, worked out from
    /// his age and his attributes as they are at this moment.
    ///
    /// It travels beside the salary and not instead of it because the two answer different
    /// questions and a renewal is the moment both are on the table: one is what he costs, the
    /// other is what he would cost. They are the same number until something changes, and the
    /// whole point of showing both is that a manager can see which of the two he is about to
    /// move.
    /// </summary>
    public decimal WageOnRenewal { get; set; }

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

    /// <summary>
    /// How many rounds of the season the world still has to play, read once for the card and
    /// handed to the settlement below.
    /// </summary>
    public int RoundsLeftInSeason { get; set; }

    /// <summary>
    /// What it would cost the club that holds him to let him go today.
    ///
    /// <para>
    /// The card used to state a flat "+20%" beside his contract. No rule in the game charges
    /// that: a release is settled at half of what the club still owes — the wages of the
    /// rounds left in this season plus the wages of every season the contract promised after
    /// it — and a percentage printed next to a player is a number a manager budgets against
    /// that the engine will never charge. It is worked out here by
    /// <see cref="NinjaEleven.Domain.Transfers.ReleaseRules"/>, the same rule the release
    /// command settles with.
    /// </para>
    /// </summary>
    public decimal ReleaseCost { get; set; }

    /// <summary>The season's line, and the career's, so a table can show both.</summary>
    public PlayerCareerLine Season { get; set; } = new();

    public PlayerCareerLine Total { get; set; } = new();

    /// <summary>
    /// Every match he has a line for, newest first. The whole career comes back and the
    /// screen filters it, because a season filter that refetched on every click would be a
    /// second source of truth about which matches count.
    /// </summary>
    public List<PlayerMatchLine> History { get; set; } = new();

    /// <summary>
    /// The same history, told one season at a time, newest first.
    ///
    /// <para>
    /// A page of matches answers "how has he been", which is a question about a run of
    /// evenings. A manager signing a striker wants to know what a season of him looks like,
    /// and that is a different question: goals, cards, knocks and results counted over a
    /// season, in the shirt he was wearing then. The rows are grouped by season <i>and</i> by
    /// club, because a transfer inside a season makes two lines of one season and one line
    /// would credit the second club with the first one's goals.
    /// </para>
    ///
    /// <para>
    /// The lines are the history's own, summed — not a second reading of the statistics
    /// table. A season's goals counted here and a season's goals counted from the rows above
    /// are the same sum of the same matches, which is the only way the two tables can be on
    /// one page without one of them being a lie.
    /// </para>
    /// </summary>
    public List<PlayerSeasonLine> Seasons { get; set; } = new();
}

/// <summary>
/// One season of a player's career, in the shirt he was wearing for it.
///
/// <para>
/// The club is part of the row's identity rather than a note on it: a career crosses clubs
/// inside a season more often than anybody expects, and a line that named only the season
/// would be a number that belongs to two men.
/// </para>
/// </summary>
public class PlayerSeasonLine
{
    public Guid? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    public Guid TeamId { get; set; }
    public string? TeamName { get; set; }

    /// <summary>What he did over those matches: games, goals, assists, saves, cards, knocks.</summary>
    public PlayerCareerLine Line { get; set; } = new();

    /// <summary>How the club he was playing for did in the matches he has a line for.</summary>
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

/// <summary>
/// What a player has done, summed over a set of matches. Appearances keep the two things a
/// single number loses apart: games started and games entered off the bench, which is what
/// "14 (3) [2]" is made of.
/// </summary>
public class PlayerCareerLine
{
    public int Appearances { get; set; }
    public int Assists { get; set; }

    /// <summary>
    /// The average of the ratings he was given, and null when he has not been rated once.
    /// Kept beside the sums rather than worked out by the screen, for the same reason every
    /// other number on this card is worked out in one place.
    /// </summary>
    public double? AverageRating { get; set; }

    /// <summary>How many matches that average stands on.</summary>
    public int RatedMatches { get; set; }

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

    /// <summary>
    /// What the match was worth to him, and null when he did not play enough of it to have
    /// one. Read from the column the match wrote, never recomputed here: the tally it was
    /// worked out from is a fact about a match that was played, and asking again would give
    /// the same fixture two different answers on two different days.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>The band that number falls in, decided by the domain and not by the client.</summary>
    public Domain.Matches.MatchRatingBand RatingBand { get; set; }

    /// <summary>Minutes actually on the pitch, which is what the recovery was measured against.</summary>
    public int MinutesPlayed { get; set; }

    public int Assists { get; set; }

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
    /// The club he played for, so a history row and a season row are doors to that club
    /// rather than names on a page.
    /// </summary>
    public Guid? TeamId { get; set; }

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
    /// <summary>
    /// What the match was worth to him, and null when he did not play enough of it to have
    /// one. Read from the column the match wrote, never recomputed here: the tally it was
    /// worked out from is a fact about a match that was played, and asking again would give
    /// the same fixture two different answers on two different days.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>The band that number falls in, decided by the domain and not by the client.</summary>
    public Domain.Matches.MatchRatingBand RatingBand { get; set; }

    public int MinutesPlayed { get; set; }
    public int Assists { get; set; }
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

        /// <summary>
        /// The club he played for, so a history line is a door to that club rather than a name
        /// on a page. It is the id the statistics row already carries, read out rather than
        /// looked up again.
        /// </summary>
        public Guid? TeamId { get; set; }
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
    /// Whether this player is an academy youth player rather than a first-team squad member.
    /// The training screen uses it to label and gate the row.
    /// </summary>
    public bool IsAcademyPlayer { get; set; }

    /// <summary>
    /// What one session on this man costs the club, being a share of his season wage.
    /// </summary>
    public decimal SessionFee { get; set; }

    public List<TrainingAttributeQuote> Attributes { get; set; } = new();
}

/// <summary>
/// The club's whole training sheet, with what the squad has between them.
///
/// <para>
/// A manager opening the training screen is asking a question about twenty-three men, and
/// asking it a man at a time would be twenty-three round trips to answer a question the
/// backend can answer in one — and the last answer would arrive after the first had already
/// been clicked, so the prices on screen would be from two different moments.
/// </para>
/// </summary>
public class SquadTrainingQuotes
{
    public Guid TeamId { get; set; }
    public Guid SeasonId { get; set; }
    public int SquadEnergy { get; set; }

    /// <summary>
    /// The calendar day the sheet is for. It is named rather than implied by the client.
    /// </summary>
    public DateOnly Day { get; set; }

    /// <summary>
    /// Whether the club has a fixture on <see cref="Day"/>, which is relevant context for a
    /// training decision.
    /// </summary>
    public bool PlaysToday { get; set; }

    public List<TrainingQuote> Players { get; set; } = new();
}
