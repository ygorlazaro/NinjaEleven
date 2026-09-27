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
    public DateOnly BirthDate { get; set; }
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

    public int Energy { get; set; }
    public bool IsAvailable { get; set; }
    public string Injury { get; set; } = string.Empty;
    public int InjuryMatchesRemaining { get; set; }

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
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public int RoundNumber { get; set; }
}

/// <summary>
    /// A player's line in a match, with the match itself already resolved.
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
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public int RoundNumber { get; set; }
    }
