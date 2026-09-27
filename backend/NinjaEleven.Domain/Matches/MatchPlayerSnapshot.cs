using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Domain.Matches;

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

    /// <summary>
    /// The energy the player has left, rounded for everything that reads or writes it. A
    /// match spends a third of a point a tick, so the engine has to keep the decimals
    /// somewhere or the cost rounds away to nothing over a season.
    /// </summary>
    public int Energy => (int)Math.Round(_energy, MidpointRounding.AwayFromZero);

    private double _energy;

    /// <summary>
    /// The energy with the decimals the engine works in.
    /// </summary>
    public double PreciseEnergy => _energy;
    public int SeasonYellowCards { get; private set; }
    public int SuspensionMatches { get; private set; }
    public int MatchYellowCards { get; set; }
    public bool RedCard { get; set; }
    public bool SubbedIn { get; set; }

    /// <summary>
    /// Whether this player was on the pitch at any point of the match. It is not the same
    /// question as "is he on the pitch now": a man who came on and went off again, or who
    /// was taken off, has played, and the difference between him and a man who sat on the
    /// bench for ninety minutes is exactly what a squad rotation is decided on.
    /// </summary>
    public bool PlayedInMatch { get; set; }

    /// <summary>
    /// Whether this player has already left the pitch and cannot be named again.
    ///
    /// Football does not give a substitute back: a manager who takes a man off has spent
    /// him. Without this the bench fills with players who are already on it — the engine
    /// would take a defender off, put a midfielder on, and then bring the defender straight
    /// back, which is the same change twice and a second one he never asked for.
    /// </summary>
    public bool SubbedOff { get; set; }

    /// <summary>
    /// The minute he came on. Zero for the eleven that started the match, which is what a
    /// starter's <see cref="MinutesPlayed"/> is measured from.
    /// </summary>
    public int EnteredAtMinute { get; set; }

    /// <summary>
    /// The minute he left the pitch, or minus one while he is still on it.
    /// </summary>
    /// <remarks>
    /// It is stamped once, whatever ended it: a substitution, a red card, a serious knock.
    /// A man who is sent off at thirty has played thirty minutes, and a card that did not
    /// stamp the minute would leave him being paid for the ninety he did not play.
    /// </remarks>
    public int LeftAtMinute { get; set; } = -1;

    /// <summary>
    /// How long he was actually on the pitch, which is the only thing a recovery can honestly
    /// be measured against.
    /// </summary>
    /// <remarks>
    /// A man who never went on has no minutes, whatever the stamps say. He started on the
    /// bench with <see cref="EnteredAtMinute"/> at zero and left it with
    /// <see cref="LeftAtMinute"/> unstamped, and taking the difference of the two would hand
    /// him a full match he never played — which is the difference between a day off and a
    /// match, and the whole reason a squad is rotated.
    /// </remarks>
    /// <param name="finalMinute">
    /// The last minute of the match. A man who never left is treated as having left at the
    /// whistle, because he did.
    /// </param>
    public int MinutesPlayed(int finalMinute)
    {
        if (!PlayedInMatch)
        {
            return 0;
        }

        var left = LeftAtMinute >= 0 ? LeftAtMinute : finalMinute;

        return Math.Max(0, left - EnteredAtMinute);
    }

    /// <summary>
    /// The season goals the player had at kick-off, plus everything he scored in this
    /// match. <see cref="MatchGoals"/> is the part of this match alone, and it is what
    /// the season state receives when the match ends.
    /// </summary>
    public int Goals { get; set; }

    public int MatchGoals { get; set; }

    /// <summary>
    /// Goals he put through his own net in this match. Not a season number: an own goal is
    /// remembered for the match it happened in, which is the only reason anybody mentions
    /// it afterwards.
    /// </summary>
    public int MatchOwnGoals { get; set; }

    /// <summary>
    /// Saves in this match. A goalkeeper's work is the one thing about a match that is
    /// counted rather than celebrated, so it is given a number of its own on his card.
    /// </summary>
    public int MatchSaves { get; set; }

    /// <summary>
    /// A player who injured himself during the match and left the pitch. He is out for
    /// the rest of the game, and the season state carries the injury forward.
    /// </summary>
    public bool InjuredOff { get; set; }

    /// <summary>
    /// Severity of the injury of this match, when there is one. It survives a knock the
    /// player played through, because it is what makes the next one more likely and every
    /// sprint after it more expensive.
    /// </summary>
    public Injury Injury { get; set; } = Injury.None;

    /// <summary>
    /// How many matches of his club a serious injury takes out. The engine draws it when
    /// the knock happens, because a month of football is not a fixed sentence.
    /// </summary>
    public int InjuryMatchesOut { get; set; }

    /// <summary>
    /// An outfield player who had to take the gloves because his club is short of
    /// goalkeepers. He is not a goalkeeper, but he defends the goal.
    /// </summary>
    public bool EmergencyGK { get; private set; }

    public bool IsOnPitch => !RedCard && !InjuredOff;

    /// <summary>
    /// Stamps the minute he left the pitch, the first time only. A man who is taken off and
    /// then sent off from the bench is not a man who played until the whistle.
    /// </summary>
    public void LeaveThePitchAt(int minute)
    {
        if (LeftAtMinute < 0)
        {
            LeftAtMinute = minute;
        }
    }

    /// <summary>
    /// The goalkeeper a club currently has on the pitch: a real one, or whoever was
    /// promoted after the last one was sent off or injured.
    /// </summary>
    public bool KeepsGoal => !RedCard && !InjuredOff && (Position == Position.GK || EmergencyGK);

    /// <summary>
    /// Leaves the pitch. An outfield player who was keeping goal does not give the gloves
    /// back: the club has no one else.
    /// </summary>
    public void SendOff(int minute)
    {
        RedCard = true;
        LeaveThePitchAt(minute);
    }

    /// <summary>
    /// A knock. Whether it costs him the rest of the match is a separate question from
    /// how bad it is, and the engine answers it on its own: a light knock is a player who
    /// stays down, a serious one is a player who leaves.
    /// </summary>
    public void Injure(Injury injury, int matchesOut = 0, int minute = 0)
    {
        Injury = injury;

        if (injury == Injury.Grave)
        {
            InjuredOff = true;
            InjuryMatchesOut = matchesOut;
            LeaveThePitchAt(minute);
        }
    }

    /// <summary>
    /// A serious injury clears when the club has played enough matches without him. The
    /// count and the severity are cleared together, because an injury with no counter
    /// left is an absence with no end.
    /// </summary>
    public void RecoverFromInjury()
    {
        Injury = Injury.None;
        InjuryMatchesOut = 0;
    }

    /// <summary>
    /// Promotes an outfield player to goalkeeper after his club lost its last one.
    /// </summary>
    public void PromoteToGoalkeeper() => EmergencyGK = true;

    /// <summary>
    /// Gives the gloves back, when a real goalkeeper arrives to take them. Called on the
    /// outgoing player of a substitution, which is the only moment it is safe: after it
    /// there is a keeper in goal who does not need them.
    /// </summary>
    public void ClearGoalkeeperDuty() => EmergencyGK = false;

    public void ApplyFatigue(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _energy = Math.Max(0, _energy - amount);
    }

    /// <summary>
    /// What a stretch of the match costs this player, down to a floor. It is the energy
    /// model: the cost rises with his age and with any knock he is playing through, and
    /// nobody plays a match on empty.
    /// </summary>
    public void DrainEnergy(double amount, int floor)
    {
        _energy = Math.Max(floor, _energy - amount);
    }

    private MatchPlayerSnapshot() { }

    public static MatchPlayerSnapshot FromPlayerSeasonState(Player player, PlayerSeasonState state)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        if (state is null) throw new ArgumentNullException(nameof(state));

        var snapshot = new MatchPlayerSnapshot
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
            SeasonYellowCards = state.YellowCards,
            SuspensionMatches = state.SuspensionMatches,
            MatchYellowCards = 0,
            RedCard = false,
            SubbedIn = false,
            SubbedOff = false,
            Goals = state.Goals,
            MatchGoals = 0,
            MatchOwnGoals = 0,
            MatchSaves = 0,
        };

        snapshot._energy = state.Energy;

        return snapshot;
    }
}