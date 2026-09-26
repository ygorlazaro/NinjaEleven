using FootballManager.Domain.Common;

namespace FootballManager.Domain.Players;

/// <summary>
/// Season-scoped mutable state for a player. Kept separate from Player so the same
/// player identity can move between clubs/seasons while accumulating stats.
/// </summary>
public class PlayerSeasonState
{
    public Guid Id { get; private set; }
    public Guid PlayerId { get; private set; }
    public Guid SeasonId { get; private set; }
    public Guid TeamId { get; private set; }

    public int Energy { get; private set; }
    public int Goals { get; private set; }
    public int YellowCards { get; private set; }
    public int RedCards { get; private set; }
    public int SuspensionMatches { get; private set; }
    public Injury Injury { get; private set; } = Injury.None;

    private PlayerSeasonState() { }

    public static PlayerSeasonState Create(
        Guid playerId,
        Guid seasonId,
        Guid teamId,
        int energy)
    {
        return new PlayerSeasonState
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            SeasonId = seasonId,
            TeamId = teamId,
            Energy = ClampEnergy(energy),
            Goals = 0,
            YellowCards = 0,
            RedCards = 0,
            SuspensionMatches = 0,
            Injury = Injury.None,
        };
    }

    private static int ClampEnergy(int value) => Math.Max(1, Math.Min(100, value));

    public void AddGoal() => Goals++;
    public void AddYellowCard()
    {
        YellowCards++;
        if (YellowCards >= 3)
        {
            SuspensionMatches = Math.Max(SuspensionMatches, 2);
            YellowCards = 0;
        }
    }
    public void AddRedCard()
    {
        RedCards++;
        SuspensionMatches = Math.Max(SuspensionMatches, 2);
    }
    public void AddSuspension(int matches) => SuspensionMatches = Math.Max(SuspensionMatches, matches);
    public void ReduceSuspension() => SuspensionMatches = Math.Max(0, SuspensionMatches - 1);
    public void SetInjury(Injury injury) => Injury = injury;
    public void RecoverEnergy(int amount) => Energy = Math.Min(100, Energy + amount);
    public void DrainEnergy(int amount) => Energy = Math.Max(1, Energy - amount);
}