using FootballManager.Domain.Common;
using FootballManager.Domain.Enums;
using FootballManager.Domain.Players;

namespace FootballManager.Domain.Matches;

/// <summary>
/// A snapshot of a player as seen by the match engine: identity + season state.
/// This is the minimal view the engine needs; it does not depend on EF Core or HTTP.
/// </summary>
public class MatchPlayerSnapshot
{
    public Guid PlayerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Age { get; private set; }
    public Position Position { get; private set; }
    public int Speed { get; private set; }
    public int Accuracy { get; private set; }
    public int Dribbling { get; private set; }
    public int Heading { get; private set; }
    public int Strength { get; private set; }
    public int GoalkeeperPower { get; private set; }
    public int Reflexes { get; private set; }
    public int Energy { get; private set; }
    public int SeasonYellowCards { get; private set; }
    public int SuspensionMatches { get; private set; }
    public int MatchYellowCards { get; set; }
    public bool RedCard { get; set; }
    public bool SubbedIn { get; set; }

    /// <summary>
    /// The season goals the player had at kick-off, plus everything he scored in this
    /// match. <see cref="MatchGoals"/> is the part of this match alone, and it is what
    /// the season state receives when the match ends.
    /// </summary>
    public int Goals { get; set; }

    public int MatchGoals { get; set; }

    /// <summary>
    /// A player who injured himself during the match and left the pitch. He is out for
    /// the rest of the game, and the season state carries the injury forward.
    /// </summary>
    public bool InjuredOff { get; set; }

    /// <summary>
    /// Severity of the injury of this match, when there is one.
    /// </summary>
    public Injury Injury { get; set; } = Injury.None;

    /// <summary>
    /// An outfield player who had to take the gloves because his club is short of
    /// goalkeepers. He is not a goalkeeper, but he defends the goal.
    /// </summary>
    public bool EmergencyGK { get; private set; }

    public bool IsOnPitch => !RedCard && !InjuredOff;

    /// <summary>
    /// The goalkeeper a club currently has on the pitch: a real one, or whoever was
    /// promoted after the last one was sent off or injured.
    /// </summary>
    public bool KeepsGoal => !RedCard && !InjuredOff && (Position == Position.GK || EmergencyGK);

    /// <summary>
    /// Leaves the pitch. An outfield player who was keeping goal does not give the gloves
    /// back: the club has no one else.
    /// </summary>
    public void SendOff() => RedCard = true;

    public void Injure(Injury injury)
    {
        InjuredOff = true;
        Injury = injury;
    }

    /// <summary>
    /// Promotes an outfield player to goalkeeper after his club lost its last one.
    /// </summary>
    public void PromoteToGoalkeeper() => EmergencyGK = true;

    public void ApplyFatigue(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Energy = Math.Max(0, Energy - amount);
    }

    private MatchPlayerSnapshot() { }

    public static MatchPlayerSnapshot FromPlayerSeasonState(Player player, PlayerSeasonState state)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (state == null) throw new ArgumentNullException(nameof(state));

        return new MatchPlayerSnapshot
        {
            PlayerId = player.Id,
            Name = player.Name,
            Age = player.CalculateAge(),
            Position = player.Position,
            Speed = player.Speed,
            Accuracy = player.Accuracy,
            Dribbling = player.Dribbling,
            Heading = player.Heading,
            Strength = player.Strength,
            GoalkeeperPower = player.GoalkeeperPower,
            Reflexes = player.Reflexes,
            Energy = state.Energy,
            SeasonYellowCards = state.YellowCards,
            SuspensionMatches = state.SuspensionMatches,
            MatchYellowCards = 0,
            RedCard = false,
            SubbedIn = false,
            Goals = state.Goals,
            MatchGoals = 0,
        };
    }
}