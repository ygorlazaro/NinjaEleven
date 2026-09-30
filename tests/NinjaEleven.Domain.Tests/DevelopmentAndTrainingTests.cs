using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Players;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A career, one year at a time: what a man is worth now, what he is worth at his best, and
/// what training costs to move him the last of the way.
///
/// <para>
/// The rules being held here are three and they are the three a growth curve can get wrong.
/// It can grow forever, so nothing a manager sees is a forecast. It can shrink everything at
/// once, so a man of thirty-eight is a one out of ten at every attribute and the engine reads
/// him as the worst player in the division rather than as an old man who is worse than he
/// was. And it can flatten, so a centre-back arrives at his potential as a man who can also
/// finish, which is not a footballer's development and is not what the numbers said would
/// happen.
/// </para>
/// </summary>
public class DevelopmentAndTrainingTests
{
    // --- The shape of a career ----------------------------------------------------

    [Fact]
    public void AYoungManGrowsAndAnOldManFalls()
    {
        var before = DevelopmentRules.Overall(APlayer(age: 17, level: 40, potential: 90));

        var atPeak = APlayer(age: 17, level: 40, potential: 90);
        for (var year = 0; year < 9; year++)
        {
            atPeak.AgeUp();
        }

        var late = APlayer(age: 17, level: 40, potential: 90);
        for (var year = 0; year < 24; year++)
        {
            late.AgeUp();
        }

        Assert.True(
            DevelopmentRules.Overall(atPeak) > before,
            "A seventeen-year-old given nine years of his career should be a better player.");

        Assert.True(
            DevelopmentRules.Overall(late) < DevelopmentRules.Overall(atPeak),
            "A man of forty-one should be a worse player than the same man at his peak.");
    }

    [Fact]
    public void ThePeakIsWhereTheCurveTurnsOver()
    {
        // The strongest attribute of the set, and the one that goes first, so the reading is a
        // direct reading of whether the man is still arriving or already leaving.
        var man = APlayer(age: 17, level: 45, potential: 95);

        var readings = new List<double>();

        for (var year = 0; year < 20; year++)
        {
            man.AgeUp();
            readings.Add(DevelopmentRules.Overall(man));
        }

        var best = readings.IndexOf(readings.Max());

        Assert.InRange(best + DevelopmentRules.YoungestAge, 22, 31);

        // He is still growing on the way up and falling on the way down, and the turn is a
        // real one rather than a plateau.
        Assert.True(readings[best] > readings[0], "He should be better at his peak than at seventeen.");
        Assert.True(readings[^1] < readings[best], "He should be worse at thirty-seven than at his peak.");
    }

    [Fact]
    public void ANobodyOutlivesHisPeak()
    {
        // A goalkeeper's peak is the latest in the game and his floor is the highest, so a
        // keeper of forty is still a recognisable keeper where a striker of forty is not.
        var keeper = APlayer(age: 17, level: 50, potential: 92, position: Position.GK);
        var striker = APlayer(age: 17, level: 50, potential: 92, position: Position.ATT);

        for (var year = 0; year < 23; year++)
        {
            keeper.AgeUp();
            striker.AgeUp();
        }

        Assert.True(
            DevelopmentRules.Overall(keeper) > DevelopmentRules.Overall(striker),
            "A keeper should outlast a striker of the same age and the same potential.");
    }

    [Fact]
    public void TheTankIsAlwaysReachableAndNeverAboveTheCeiling()
    {
        // The bands are read off the curve rather than invented beside it: a man the world
        // draws at twenty and a man it draws at twenty-six have to be on the same curve, or
        // the veterans are seeded above the ceiling their own development can reach.
        foreach (var age in new[] { 16, 19, 22, 25, 28, 31, 34, 38, 44, 55 })
        {
            var man = APlayer(age: age, level: 45, potential: 80, stamina: 50);

            for (var year = 0; year < 8; year++)
            {
                man.AgeUp();
            }

            Assert.InRange(man.Stamina, 1, (int)Math.Ceiling(DevelopmentRules.StaminaCeiling));
        }
    }

    [Fact]
    public void ADeclineSettlesOntoAFloorRatherThanRunningToTheBottom()
    {
        var man = APlayer(age: 30, level: 70, potential: 72);

        for (var year = 0; year < 20; year++)
        {
            man.AgeUp();
        }

        foreach (var attribute in DevelopmentRules.All)
        {
            // A centre-back has no reflexes and never had any, and a rule that held the
            // floor over them would be holding a floor over two zeroes.
            if (attribute is PlayerAttribute.GoalkeeperPower or PlayerAttribute.Reflexes)
            {
                Assert.Equal(0, man.Get(attribute));
                continue;
            }

            Assert.InRange(
                man.Get(attribute),
                (int)DevelopmentRules.DeclineFloor(attribute),
                100);
        }
    }

    // --- The ceiling --------------------------------------------------------------

    [Fact]
    public void NobodyIsGrownPastHisPotential()
    {
        // Every age, and every position, because a cap that only holds for the man the curve
        // was drawn with is not a cap.
        for (var age = DevelopmentRules.YoungestAge; age <= 40; age++)
        {
            foreach (var position in new[] { Position.GK, Position.DEF, Position.MID, Position.ATT })
            {
                var man = APlayer(age: 16, level: 30, potential: 64, position: position);

                for (var year = 0; year < 30; year++)
                {
                    man.AgeUp();

                    Assert.InRange(
                        DevelopmentRules.Overall(man),
                        1.0,
                        64.0 + 0.5);
                }
            }
        }
    }

    [Fact]
    public void AProdigyKeepsTheShapeHeWasDrawnWith()
    {
        // A centre-back who is better at heading than at finishing, drawn to potential: the
        // development has to spend his headroom where his position says, and a model that
        // grew every attribute towards its own ceiling would have handed him a hundred at
        // dribbling and called it a centre-back.
        var man = Player.Create(
            "Zé da Silva", 16, Position.DEF,
            speed: 55, accuracy: 22, dribbling: 20, heading: 60, strength: 58,
            goalkeeperPower: 0, reflexes: 0, stamina: 70, potential: 84);

        for (var year = 0; year < 12; year++)
        {
            man.AgeUp();
        }

        Assert.True(man.Heading > man.Speed, "His heading should still be his best attribute.");
        Assert.True(man.Dribbling < man.Speed, "His dribbling should still be his worst.");
        Assert.InRange(man.Accuracy, 20, 40);
    }

    [Fact]
    public void TheCeilingFallsWithAge()
    {
        // A man who is twenty-eight has a body that has not started going; a man who is
        // thirty-eight has one that has, and the world drawing a prospect at thirty-eight is
        // a world whose prospects are all thirty-eight.
        var young = DevelopmentRules.CeilingFor(20);
        var old = DevelopmentRules.CeilingFor(40);

        Assert.True(young > old, "The ceiling should fall with age.");
        Assert.Equal(100, DevelopmentRules.CeilingFor(17));
    }

    [Fact]
    public void APotentialIsNeverBelowWhereTheManAlreadyIs()
    {
        // The one value that would freeze him: a ceiling under his own attributes means no
        // growth, ever, for a man the world has decided is finished at twenty-six.
        for (var age = 16; age <= 50; age++)
        {
            for (var roll = 0.0; roll < 1.0; roll += 0.05)
            {
                var reading = 30.0 + roll * 60.0;

                var potential = DevelopmentRules.PotentialFor(reading, age, roll);

                // A man already better than the top his age allows is a man the world expects
                // to have finished, so his ceiling is where he is rather than below it. That
                // is the one case where the potential sits above the age's ceiling, and it is
                // the case that used to throw: Math.Clamp with the bounds the wrong way round.
                var top = Math.Max(DevelopmentRules.CeilingFor(age), reading);

                Assert.InRange(potential, (int)Math.Floor(reading), (int)Math.Ceiling(top));
            }
        }
    }

    [Fact]
    public void MostPlayersHaveRoomLeftAndAFewHaveNearlyAllOfIt()
    {
        // The spread is the point. A man of forty-five at eighteen with a potential of
        // seventy-four and a man of forty-five at eighteen with a potential of ninety-eight
        // are two different signings, and a draw that gave them the same expectation would
        // have made the market a list where every prospect looked like the same prospect.
        var room = new List<double>();

        for (var roll = 0.0; roll < 1.0; roll += 0.01)
        {
            room.Add(DevelopmentRules.PotentialFor(45.0, 18, roll) - 45.0);
        }

        var sorted = room.OrderBy(value => value).ToList();
        var median = sorted[sorted.Count / 2];

        // The many land in the middle of the room, and the few reach nearly all of it.
        Assert.InRange(median, 12.0, 32.0);
        Assert.True(sorted[^1] > 45.0, "Some men should be left with almost the whole scale in front of them.");

        // And no man is left with nothing: even the unluckiest draw leaves a fifth of the
        // room, which is what keeps a low draw from freezing a player the world just created.
        Assert.True(sorted[0] > 0.0, "A man should never be created already at his ceiling.");
    }

    // --- The tank -----------------------------------------------------------------

    [Fact]
    public void TheTankPeaksWithTheBodyAndEmptiesAfterIt()
    {
        var man = APlayer(age: 16, level: 40, potential: 80, stamina: 50);

        var readings = new List<int>();

        for (var year = 0; year < 20; year++)
        {
            man.AgeUp();
            readings.Add(man.Stamina);
        }

        var best = readings.IndexOf(readings.Max());

        // readings[0] is the man at seventeen, having already had a year, so the age is the
        // index plus seventeen. The peak is a short plateau rather than a single year — the
        // fill is exponential and the momentum has all but run out by then — so the assertion
        // is about where it turns over, not about which year of the plateau is named.
        Assert.InRange(best + 17, DevelopmentRules.StaminaPeakAge - 3, DevelopmentRules.StaminaPeakAge);

        // And the plateau is near the ceiling, which is the claim the rate exists to support.
        Assert.True(
            readings[best] >= 0.9 * DevelopmentRules.StaminaCeiling,
            $"A body at its peak should be near its ceiling, was {readings[best]} of {DevelopmentRules.StaminaCeiling}.");
        Assert.True(readings[0] < readings[best], "His tank should fill before it empties.");
        Assert.True(readings[^1] < readings[best], "His tank should be spent by thirty-six.");
    }

    [Fact]
    public void TheTankIsNotPartOfTheRating()
    {
        // Stamina is a body fact and is not in the reading his potential is a ceiling for. Two
        // men who are the same footballer and who differ only in the tank must read the same,
        // or a man whose tank filled would be a man whose rating rose without his football
        // having improved at all.
        var lean = APlayer(age: 16, level: 40, potential: 80, stamina: 40);
        var fat = APlayer(age: 16, level: 40, potential: 80, stamina: 100);

        for (var year = 0; year < 8; year++)
        {
            lean.AgeUp();
            fat.AgeUp();
        }

        Assert.Equal(DevelopmentRules.Overall(lean), DevelopmentRules.Overall(fat), 6);
        Assert.True(fat.Stamina > lean.Stamina, "Their tanks should still be nowhere near each other.");
    }

    // --- Training -----------------------------------------------------------------

    [Fact]
    public void TheLastPointsOfACareerCostTheMost()
    {
        var man = APlayer(age: 20, level: 50, potential: 50);

        man.Set(PlayerAttribute.Heading, 20);
        var cheapest = TrainingRules.Cost(man, PlayerAttribute.Heading);

        man.Set(PlayerAttribute.Heading, 35);
        var middling = TrainingRules.Cost(man, PlayerAttribute.Heading);

        man.Set(PlayerAttribute.Heading, 49);
        var dearest = TrainingRules.Cost(man, PlayerAttribute.Heading);

        Assert.True(dearest > middling && middling > cheapest, "The price should climb towards the ceiling.");
        Assert.InRange(cheapest, TrainingRules.MinimumCost, TrainingRules.MaximumCost);
        Assert.InRange(dearest, TrainingRules.MinimumCost, TrainingRules.MaximumCost);
    }

    [Fact]
    public void APlayerIsNeverTrainedPastHisPotential()
    {
        var man = APlayer(age: 22, level: 50, potential: 60);

        Assert.False(TrainingRules.IsAtCeiling(man, PlayerAttribute.Strength));

        man.Set(PlayerAttribute.Strength, 60);

        Assert.True(TrainingRules.IsAtCeiling(man, PlayerAttribute.Strength));
    }

    [Fact]
    public void AnOutfielderHasNoGoalkeepingToTrain()
    {
        var outfielder = APlayer(age: 22, level: 50, potential: 70);
        var keeper = APlayer(age: 22, level: 50, potential: 70, position: Position.GK);

        Assert.False(TrainingRules.BelongsToThisMan(outfielder, PlayerAttribute.Reflexes));
        Assert.False(TrainingRules.BelongsToThisMan(outfielder, PlayerAttribute.GoalkeeperPower));

        Assert.True(TrainingRules.BelongsToThisMan(keeper, PlayerAttribute.Reflexes));
        Assert.True(TrainingRules.BelongsToThisMan(keeper, PlayerAttribute.GoalkeeperPower));

        // A keeper's legs are his own and the engine reads them in a reshuffle, so refusing
        // them would be a rule about a different game.
        Assert.True(TrainingRules.BelongsToThisMan(keeper, PlayerAttribute.Speed));
    }

    [Fact]
    public void ATiredManPaysMoreToTrain()
    {
        // The tank that decides what a match costs him decides what a session costs him, which
        // is the reason the two cannot disagree.
        var fresh = APlayer(age: 22, level: 50, potential: 70, stamina: 90);
        var spent = APlayer(age: 22, level: 50, potential: 70, stamina: 10);

        Assert.True(
            TrainingRules.Cost(spent, PlayerAttribute.Strength) > TrainingRules.Cost(fresh, PlayerAttribute.Strength),
            "A man who empties easily should pay more for the same session.");
    }

    [Fact]
    public void TheReferencePlayerPaysTheReferencePrice()
    {
        // The same guard the stamina rules carry: a training price that quietly taxed the
        // average player would be a rounding decision nobody had made.
        var man = APlayer(age: 22, level: Player.PotentialReference, potential: Player.PotentialReference, stamina: Player.StaminaReference);

        var expected = TrainingRules.MinimumCost
            + (TrainingRules.MaximumCost - TrainingRules.MinimumCost);

        Assert.Equal(expected, TrainingRules.Cost(man, PlayerAttribute.Strength));
    }

    [Fact]
    public void DevelopmentIsAResultAndNotAFormulaRunByTheCaller()
    {
        // Ageing and growing are one call, because a caller that could forget the growth
        // would produce a world where a man gets a year older and no different for it.
        var man = APlayer(age: 20, level: 45, potential: 80);

        man.AgeUp();

        Assert.Equal(21, man.Age);
        Assert.True(DevelopmentRules.Overall(man) > 45.0);
    }

    // --- Helpers ------------------------------------------------------------------

    private static Player APlayer(
        int age,
        int level,
        int potential,
        Position position = Position.MID,
        int stamina = 70) =>
        Player.Create(
            "Teste", age, position,
            speed: level, accuracy: level, dribbling: level, heading: level, strength: level,
            goalkeeperPower: position == Position.GK ? level : 0,
            reflexes: position == Position.GK ? level : 0,
            stamina: stamina,
            potential: potential);
}
