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

    /// <summary>
    /// The club the player is on this season, or null when he is a free agent. A player with
    /// no club has a season state — his goals and knocks still belong to him — and the
    /// state says so rather than omitting itself, because a market that could not ask a
    /// man what he did last year could not ask him what he is worth this one.
    /// </summary>
    public Guid? TeamId { get; private set; }

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

    /// <summary>
    /// Whether the player has declared he will retire at the end of this season. A man
    /// between thirty-seven and forty-two may say so at the start of a season, and the flag
    /// is what the squad table and the profile show: a shirt with a man walking away from it
    /// at the end of the year is a fact about the man, and it is a fact the club has to plan
    /// around, so it belongs to the season state and not to a screen.
    /// </summary>
    public bool Retiring { get; private set; }

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

    /// <summary>
    /// A season state for a player who is not on a club. The state still carries his goals
    /// and his knocks, because a free agent's worth is read from the season he has had and
    /// not from the fact that he is looking for work.
    /// </summary>
    public static PlayerSeasonState CreateFreeAgent(
        Guid playerId,
        Guid seasonId,
        int energy)
    {
        return new PlayerSeasonState
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            SeasonId = seasonId,
            TeamId = null,
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

    /// <summary>
    /// Moves the player to a club, or releases him from one. A transfer is the moment a man
    /// changes shirts, and this is the line that says so: the state's club is the club he is
    /// playing for, and a man who has been sold has a new one.
    /// </summary>
    public void SetTeam(Guid? teamId) => TeamId = teamId;

    /// <summary>
    /// Clears the retirement flag at the start of a new season. A man who retired last year
    /// is not retiring this one, and the flag is about the season he is about to play.
    /// </summary>
    public void ClearRetirement() => Retiring = false;

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
    /// Marks the player as retiring at the end of the season, or clears the mark. Only a man
    /// of the age that may retire may be marked, and the mark is a season's: it is reset at
    /// the start of the next one, because a retirement is a thing a man decides about the
    /// year he is about to play and not a thing that follows him into the next.
    /// </summary>
    public void SetRetiring(bool retiring, int age)
    {
        if (retiring && (age < RetirementRules.MinRetirementAge || age > RetirementRules.MaxRetirementAge))
        {
            throw new ArgumentOutOfRangeException(
                nameof(age),
                age,
                $"A player of {age} cannot declare he is retiring; only men between " +
                $"{RetirementRules.MinRetirementAge} and {RetirementRules.MaxRetirementAge} may.");
        }

        Retiring = retiring;
    }

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