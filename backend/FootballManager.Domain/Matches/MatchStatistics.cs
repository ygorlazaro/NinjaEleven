namespace FootballManager.Domain.Matches;

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
