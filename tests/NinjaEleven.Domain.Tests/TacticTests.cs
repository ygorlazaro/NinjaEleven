using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The tactic catalogue: a manager orders a shape, and the shape decides which men are
/// picked for which line.
///
/// The claim these lock is the one the lineup screen makes. If a tactic only changed a
/// label, choosing 4-2-3-1 and choosing 4-4-2 would produce the same eleven and the
/// manager's decision would be a decoration. The two shapes have the same numbers in two
/// of the three lines and still differ, because one of the five is a holding midfielder
/// and the other is not — so a squad made of the same men has to come out differently.
/// </summary>
public class TacticTests
{
    /// <summary>
    /// Every tactic in the catalogue has to be a whole eleven. A shape that does not add
    /// up to eleven is a typo, not an option, and the one place that catches it is the
    /// catalogue itself.
    /// </summary>
    [Fact]
    public void Every_tactic_is_a_whole_eleven_with_one_keeper()
    {
        Assert.NotEmpty(Tactics.All);

        foreach (var tactic in Tactics.All)
        {
            Assert.True(tactic.IsComplete, $"{tactic.Code} tem {tactic.Size} jogadores.");
            Assert.Equal(11, tactic.Size);
            Assert.Equal(1, tactic.Lines.Count(line => line.Position == Position.GK));
            Assert.Equal(10, tactic.Size - tactic.Lines.Count(line => line.Position == Position.GK));
        }
    }

    /// <summary>
    /// Ten of them, and no two the same eleven to put out. Three of the ten are 4-5-1 by
    /// the three numbers, which is why the check is on the lines rather than on the totals:
    /// what decides who plays is which line is four men wide and which is one, and 4-2-3-1
    /// and 4-1-4-1 fill the eleven differently while both counting as 4-5-1.
    /// </summary>
    [Fact]
    public void The_catalogue_offers_ten_shapes_no_two_of_which_fill_the_same_eleven()
    {
        Assert.Equal(10, Tactics.All.Count);

        var fills = Tactics.All
            .Select(tactic => string.Join("|", tactic.Lines.Select(line => $"{line.Position}:{line.Count}:{line.Profile}")))
            .ToList();

        Assert.Equal(fills.Count, fills.Distinct().Count());
    }

    [Fact]
    public void Codes_are_found_regardless_of_case_or_padding()
    {
        Assert.Equal("4231", Tactics.Find("4231")?.Code);
        Assert.Equal("4231", Tactics.Find("  4231 ")?.Code);
        Assert.Null(Tactics.Find("9999"));
        Assert.Null(Tactics.Find(null));
        Assert.Null(Tactics.Find("   "));
    }

    /// <summary>
    /// A holding midfielder is not an attacking one, and a 4-2-3-1 and a 4-4-2 with the
    /// same names available are not the same eleven. The bias has to be enough to pick a
    /// different man and small enough not to ignore ability altogether.
    /// </summary>
    [Fact]
    public void A_line_is_filled_by_the_men_who_are_best_at_that_line_s_job()
    {
        // Two midfielders of the same squad, both midfielders, one built to hold and one
        // built to get forward. The gap is a few points, the way two men in one real squad
        // differ — not the twenty a striker and a centre half would.
        var holder = Snapshot(Position.MID, speed: 12, accuracy: 13, dribbling: 11, strength: 15, heading: 14);
        var creator = Snapshot(Position.MID, speed: 15, accuracy: 15, dribbling: 16, strength: 11, heading: 10);

        Assert.True(
            PlayerMetric.TacticalMetric(creator, TacticProfile.Attacking)
            > PlayerMetric.TacticalMetric(holder, TacticProfile.Attacking),
            "A linha que cria tem de sair com quem cria.");

        Assert.True(
            PlayerMetric.TacticalMetric(holder, TacticProfile.Defensive)
            > PlayerMetric.TacticalMetric(creator, TacticProfile.Defensive),
            "A linha que segura tem de sair com quem segura.");
    }

    /// <summary>
    /// A tactic is a preference, not a filter. A manager who orders a holding midfielder
    /// and has only attacking ones should still get one, because a full eleven of the best
    /// men available beats a correct shape picked out of the wrong ones.
    /// </summary>
    [Fact]
    public void A_line_s_job_never_makes_a_man_unpickable()
    {
        var creator = Snapshot(Position.MID, speed: 15, accuracy: 15, dribbling: 16, strength: 11, heading: 10);
        var allRounder = Snapshot(Position.MID, speed: 16, accuracy: 16, dribbling: 15, strength: 14, heading: 14);

        // Told to hold, a man who is simply the best midfielder available still starts.
        Assert.True(
            PlayerMetric.TacticalMetric(allRounder, TacticProfile.Defensive)
            > PlayerMetric.TacticalMetric(creator, TacticProfile.Defensive));

        // And he is never demoted below being in the squad at all.
        Assert.True(PlayerMetric.TacticalMetric(creator, TacticProfile.Defensive) > 0);
    }

    /// <summary>
    /// The eleven is still measured on who is playing, not on what was ordered. A manager
    /// who hand-picks something the catalogue has no name for gets measured on what he
    /// actually did, which is the rule the whole model rests on.
    /// </summary>
    [Fact]
    public void A_picked_eleven_measures_as_itself_and_not_as_the_order()
    {
        var eleven = Enumerable.Range(0, 11)
            .Select(index => Snapshot(index switch
            {
                0 => Position.GK,
                <= 3 => Position.DEF,
                <= 7 => Position.MID,
                _ => Position.ATT
            }))
            .ToList();

        var played = eleven.ToDictionary(player => player.PlayerId);

        // What the engine measures is the eleven, and the eleven is this shape whichever
        // tactic the manager ordered. The tactic chose the men; it does not relabel them.
        Assert.Equal(new Formation(3, 4, 3), Formation.FromComposition(played.Values));
        Assert.Equal(
            (Tactics.Find("343")!.Defenders, Tactics.Find("343")!.Midfielders, Tactics.Find("343")!.Attackers),
            (3, 4, 3));
    }

    [Fact]
    public void A_club_reduced_to_ten_players_is_still_playing_the_shape_it_fielded()
    {
        // A striker carried off, a defender sent off, and the shape is still 3-4-3. How many
        // men a club has is the strength's business; the shape it is playing is the team
        // sheet's, and reading it off the survivors told a manager who ordered 3-4-3 that his
        // club had come out in another one.
        var eleven = Enumerable.Range(0, 11)
            .Select(index => Snapshot(index switch
            {
                0 => Position.GK,
                <= 3 => Position.DEF,
                <= 7 => Position.MID,
                _ => Position.ATT
            }))
            .ToList();

        eleven[10].SendOff(30);
        eleven[9].Injure(Injury.Grave, matchesOut: 3, minute: 55);

        var played = eleven.ToDictionary(player => player.PlayerId);

        Assert.Equal(new Formation(3, 4, 3), Formation.FromComposition(played.Values));
        Assert.Equal(10, Formation.FromComposition(played.Values).Outfielders);
    }

    private static MatchPlayerSnapshot Snapshot(
        Position position,
        int speed = 12,
        int accuracy = 12,
        int dribbling = 12,
        int heading = 12,
        int strength = 12,
        int energy = 100) =>
        MatchPlayerSnapshot.FromPlayerSeasonState(
            NinjaEleven.Domain.Players.Player.Create(
                "Jogador",
                24,
                position,
                speed: speed,
                accuracy: accuracy,
                dribbling: dribbling,
                heading: heading,
                strength: strength,
                goalkeeperPower: position == Position.GK ? 18 : 0,
                reflexes: position == Position.GK ? 18 : 0),
            NinjaEleven.Domain.Players.PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), energy));
}
