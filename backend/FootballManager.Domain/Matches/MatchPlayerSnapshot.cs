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
    public bool EmergencyGK { get; set; }
    public bool SubbedIn { get; set; }
    public int Goals { get; set; }

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
            EmergencyGK = false,
            SubbedIn = false,
            Goals = state.Goals,
        };
    }
}