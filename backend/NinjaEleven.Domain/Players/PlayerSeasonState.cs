using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Players;

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

    /// <summary>
    /// Saves made for his club this season. It is a counter of its own for the same reason
    /// the goals are: a squad table reads the season's own tally, and it must not be
    /// reassembled a second way on one screen and another way on the next.
    /// </summary>
    public int Saves { get; private set; }
    public int YellowCards { get; private set; }
    public int RedCards { get; private set; }
    public int SuspensionMatches { get; private set; }
    public Injury Injury { get; private set; } = Injury.None;

    /// <summary>
    /// How many matches of his club the player still has to miss because of the injury.
    /// A severity without a duration would be a permanent absence, so the two travel
    /// together: the severity says how it reads, the counter says for how long.
    /// </summary>
    public int InjuryMatchesRemaining { get; private set; }

    /// <summary>
    /// How many times he has been injured this season, which is not the same thing as the
    /// injury he is carrying: a player who was hurt in the first matchday and came back has
    /// no injury and has still been hurt. The count is what the market reads, because a club
    /// signing a man pays for the season he has had and not for the bandage on him today.
    /// </summary>
    public int Injuries { get; private set; }

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
            Saves = 0,
            YellowCards = 0,
            RedCards = 0,
            SuspensionMatches = 0,
            Injury = Injury.None,
            InjuryMatchesRemaining = 0,
            Injuries = 0,
        };
    }

    private static int ClampEnergy(int value) => Math.Max(1, Math.Min(100, value));

    /// <summary>
    /// A player can be picked for a match only when he is neither injured nor serving
    /// a suspension. Availability is a domain rule, so it lives with the state.
    /// </summary>
    public bool IsAvailable => Injury == Injury.None && SuspensionMatches == 0;

    public void AddGoal() => Goals++;

    public void AddSave() => Saves++;
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

    /// <summary>
    /// Registers an injury sustained in a match. A grave injury keeps the player out for
    /// longer; a light one he plays through in a couple of matches.
    /// </summary>
    public void AddInjury(Injury injury, int matches)
    {
        if (injury == Injury.None)
        {
            return;
        }

        // Every knock counts, whatever the worst of the season turns out to be. The injury he
        // carries is the worst one and only one of them; the market is told how many times he
        // was hurt, which is a different number and a bigger one.
        Injuries++;

        // The worst injury of a season is the one that counts.
        if (injury > Injury && InjuryMatchesRemaining < matches)
        {
            Injury = injury;
            InjuryMatchesRemaining = matches;
        }
        else if (injury == Injury)
        {
            InjuryMatchesRemaining = Math.Max(InjuryMatchesRemaining, matches);
        }
    }

    public void SetInjury(Injury injury) => Injury = injury;

    /// <summary>
    /// One match of his club has been played: a suspension or an injury costs one match
    /// off its counter, and the player is fit again when the counter reaches zero.
    /// </summary>
    public void RecoverFromMatches()
    {
        if (SuspensionMatches > 0)
        {
            SuspensionMatches--;
        }

        if (InjuryMatchesRemaining > 0)
        {
            InjuryMatchesRemaining--;
        }

        // An injury left over from a season state written before injuries had a duration
        // carries no counter, so it would keep a player out forever. Reaching this point
        // means his club has played a match: he is fit again.
        if (Injury != Injury.None && InjuryMatchesRemaining == 0)
        {
            Injury = Injury.None;
        }
    }

    public void SetEnergy(int energy) => Energy = Math.Clamp(energy, 1, 100);
    public void RecoverEnergy(int amount) => Energy = Math.Min(100, Energy + amount);
    public void DrainEnergy(int amount) => Energy = Math.Max(1, Energy - amount);
}