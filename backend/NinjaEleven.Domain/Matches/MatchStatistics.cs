namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Structured match statistics. These are numbers, never narration text: the
/// frontend must never have to parse a description to know the score of shots.
/// </summary>
public class MatchStatistics
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }

    public int HomePossession { get; private set; }
    public int AwayPossession { get; private set; }
    public int HomeShots { get; private set; }
    public int AwayShots { get; private set; }
    public int HomeShotsOnTarget { get; private set; }
    public int AwayShotsOnTarget { get; private set; }
    public int HomeCorners { get; private set; }
    public int AwayCorners { get; private set; }
    public int HomeFouls { get; private set; }
    public int AwayFouls { get; private set; }
    public int HomeYellowCards { get; private set; }
    public int AwayYellowCards { get; private set; }
    public int HomeRedCards { get; private set; }
    public int AwayRedCards { get; private set; }
    public int HomeSaves { get; private set; }
    public int AwaySaves { get; private set; }
    public int HomeGoals { get; private set; }
    public int AwayGoals { get; private set; }
    public int HomeSubstitutions { get; private set; }
    public int AwaySubstitutions { get; private set; }

    /// <summary>
    /// Goals each side put through its own net, and the injuries each side suffered.
    ///
    /// They are stored because a matchday is watched while it happens and read after it
    /// ended, and a scoreboard that can only say 1 x 0 cannot answer the two questions a
    /// manager actually asks about a game he did not watch: who made a mess of it, and who
    /// got hurt.
    /// </summary>
    public int HomeOwnGoals { get; private set; }
    public int AwayOwnGoals { get; private set; }
    public int HomeInjuries { get; private set; }
    public int AwayInjuries { get; private set; }

    /// <summary>
    /// The shape each side played, as three numbers on a team sheet. It is stored because
    /// a results screen that says 4-3-3 for a match that was played 4-4-2 is simply lying,
    /// and the live session is gone by the time anybody reads the result.
    /// </summary>
    public string HomeFormation { get; private set; } = string.Empty;

    public string AwayFormation { get; private set; } = string.Empty;

    private MatchStatistics() { }

    public static MatchStatistics Create(Guid matchId)
    {
        return new MatchStatistics
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            HomePossession = 50,
            AwayPossession = 50
        };
    }

    public void AddOwnGoal(bool isHome)
    {
        if (isHome) HomeOwnGoals++;
        else AwayOwnGoals++;
    }

    public void AddInjury(bool isHome)
    {
        if (isHome) HomeInjuries++;
        else AwayInjuries++;
    }

    public void SetFormations(Formation home, Formation away)
    {
        HomeFormation = home.ToString();
        AwayFormation = away.ToString();
    }

    public void SetPossession(int homePossession)
    {
        homePossession = Math.Max(0, Math.Min(100, homePossession));
        HomePossession = homePossession;
        AwayPossession = 100 - homePossession;
    }

    public void AddShot(bool isHome, bool onTarget)
    {
        if (isHome)
        {
            HomeShots++;
            if (onTarget) HomeShotsOnTarget++;
        }
        else
        {
            AwayShots++;
            if (onTarget) AwayShotsOnTarget++;
        }
    }

    public void AddCorner(bool isHome)
    {
        if (isHome) HomeCorners++;
        else AwayCorners++;
    }

    public void AddFoul(bool isHome)
    {
        if (isHome) HomeFouls++;
        else AwayFouls++;
    }

    public void AddCard(bool isHome, bool isRed)
    {
        if (isHome)
        {
            if (isRed) HomeRedCards++;
            else HomeYellowCards++;
        }
        else
        {
            if (isRed) AwayRedCards++;
            else AwayYellowCards++;
        }
    }

    public void AddSave(bool isHome)
    {
        if (isHome) HomeSaves++;
        else AwaySaves++;
    }

    public void AddGoal(bool isHome)
    {
        if (isHome) HomeGoals++;
        else AwayGoals++;
    }

    public void AddSubstitution(bool isHome)
    {
        if (isHome) HomeSubstitutions++;
        else AwaySubstitutions++;
    }
}
