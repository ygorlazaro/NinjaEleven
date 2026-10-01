using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;

using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A player's card: what he is worth after ninety minutes.
///
/// <para>
/// Three rules carry the whole thing, and each of them is a trap the obvious version falls
/// into. A rating must reward the man for <i>having scored</i> at full weight, because
/// discounting a striker's goal for being a striker means the best striker in the world cannot
/// have a good match. It must read <i>how well</i> he did from the chance the engine itself
/// gave him, because the count of goals cannot tell a clinical afternoon from a lucky one. And
/// it must not punish a centre-back for an evening on which nobody remembered he was there,
/// because that is what playing well looks like from the back four.
/// </para>
/// </summary>
public class MatchRatingTests
{
    private const int AFullMatch = 90;

    // --- The baseline ------------------------------------------------------------

    [Fact]
    public void AManWhoDidNothingWrongAndNothingRightIsMarkedAsAnOrdinaryEvening()
    {
        // The baseline is what everybody starts from, and it is not zero. A man who ran a
        // full match without being the worst player on a pitch full of footballers did not
        // play badly, and a scale that says he did is measuring something else.
        var player = ASnapshotOf(Position.MID, teamId: ATeam());

        player.PlayedInMatch = true;
        AnOrdinaryEveningOn(player);

        Assert.Equal(MatchRules.RatingBaseline, MatchRating.Of(player, AFullMatch, AFullMatch));
    }

    [Fact]
    public void ACameoIsMeasuredOnItsOwnMinutesAndNotOnTheMatchHeSatOut()
    {
        // The complaint every manager makes about a substitute nobody notices. His minutes are
        // his own, so the floor he has to clear is a fifth of the floor and the seventy
        // minutes he was not on the pitch are not held against him; the card is a reading of
        // the evening he had rather than of the evening he was given.
        var cameo = ASnapshotOf(Position.ATT, teamId: ATeam());
        cameo.PlayedInMatch = true;
        cameo.EnteredAtMinute = 70;
        cameo.MatchGoals = 1;
        cameo.Performance.RecordShot(onTarget: true, chance: 0.4);
        cameo.Performance.RecordInvolvement();

        var allOfIt = ASnapshotOf(Position.ATT, teamId: ATeam());
        allOfIt.PlayedInMatch = true;
        allOfIt.MatchGoals = 1;
        allOfIt.Performance.RecordShot(onTarget: true, chance: 0.4);
        allOfIt.Performance.RecordInvolvement();

        var twentyMinutes = MatchRating.Of(cameo, minutesPlayed: 20, totalMinutes: AFullMatch);
        var ninetyMinutes = MatchRating.Of(allOfIt, AFullMatch, AFullMatch);

        Assert.NotNull(twentyMinutes);
        Assert.True(
            Math.Abs(twentyMinutes!.Value - ninetyMinutes!.Value) < 0.05,
            "The same goal in the same evening of work is the same evening, over twenty minutes or ninety.");
    }

    // --- The five minutes --------------------------------------------------------

    [Fact]
    public void AManWhoBarelyCameOnIsNotGivenANumberAtAll()
    {
        // A floor on the sample rather than a judgement about the player. He has not had a
        // performance, he has had a cameo, and a number beside his name would be a number
        // nobody could have earned.
        var player = ASnapshotOf(Position.ATT, teamId: ATeam());
        player.PlayedInMatch = true;
        player.EnteredAtMinute = 88;

        Assert.Null(MatchRating.Of(player, minutesPlayed: 2, totalMinutes: AFullMatch));
    }

    [Fact]
    public void FiveMinutesIsEnoughAndFourIsNot()
    {
        // The line itself, read from both sides. A rule that cannot be checked from both sides
        // is a rule that will be off by one forever.
        var player = ASnapshotOf(Position.ATT, teamId: ATeam());
        player.PlayedInMatch = true;
        player.EnteredAtMinute = 85;

        Assert.Null(MatchRating.Of(player, minutesPlayed: 4, totalMinutes: AFullMatch));
        Assert.NotNull(MatchRating.Of(player, minutesPlayed: 5, totalMinutes: AFullMatch));
    }

    [Fact]
    public void AManWhoNeverLeftTheBenchIsNotGivenANumber()
    {
        // The stamps alone would say he played ninety: he started on the bench with his entry
        // minute at zero and never left it. This is the difference between a day off and a
        // match, and the same reason a squad is rotated.
        var player = ASnapshotOf(Position.ATT, teamId: ATeam());
        player.EnteredAtMinute = 0;
        player.LeftAtMinute = -1;

        Assert.Equal(0, player.MinutesPlayed(AFullMatch));
        Assert.Null(MatchRating.Of(player, player.MinutesPlayed(AFullMatch), AFullMatch));
    }

    // --- What he did -------------------------------------------------------------

    [Fact]
    public void AGoalIsWorthWhatAGoalIsWorthWhateverTheManWhoScoredIt()
    {
        // The trap this guards. A rating that charged a striker less for scoring because he is
        // a striker has made the best finisher in the world the man least able to have a good
        // match, which is a rule that measures pedigree instead of performance.
        var striker = ASnapshotOf(Position.ATT, teamId: ATeam());
        var defender = ASnapshotOf(Position.DEF, teamId: ATeam());

        foreach (var player in new[] { striker, defender })
        {
            player.PlayedInMatch = true;
            player.MatchGoals = 1;
            player.Performance.RecordShot(onTarget: true, chance: 0.5);
            player.Performance.RecordInvolvement();
        }

        var fromTheStriker = MatchRating.Of(striker, AFullMatch, AFullMatch);
        var fromTheDefender = MatchRating.Of(defender, AFullMatch, AFullMatch);

        // The same goal, the same shot and the same evening of work, so the same number. This
        // is the stronger form of the claim and the one worth holding: a rating that read a
        // striker's goal at a discount for being a striker would make the best finisher in
        // the world the man least able to have a good match, and the discount would be a
        // measurement of pedigree wearing the costume of a measurement of performance.
        Assert.True(fromTheStriker > MatchRules.RatingBaseline);
        Assert.Equal(fromTheStriker, fromTheDefender);
    }

    [Fact]
    public void ABraceIsWorthAboutTwiceASingleAndAHatTrickIsTheCeiling()
    {
        // The scale has to actually be used. A goal worth so little that two of them do not
        // leave the middle band is a rating that cannot tell a good evening from an ordinary
        // one, and the bands are the thing a manager reads.
        Assert.True(ForGoals(1) > MatchRules.RatingBaseline);
        Assert.True(ForGoals(2) > ForGoals(1));

        // Three goals is the ceiling, and it is meant to be: a hat-trick is the best thing
        // that happens to a team in a match, and a scale that needed a good evening on top of
        // it before it would say so would be pricing the hat-trick below the hat-trick. A
        // single goal is well short of it, which is the part that has to stay true.
        Assert.Equal(MatchRatingBand.Diamond, MatchRating.BandOf(ForGoals(3)));
        Assert.Equal(MatchRules.RatingCeiling, ForGoals(3));
        Assert.True(ForGoals(2) < MatchRules.RatingCeiling,
            "Two goals is a very good evening and not the best one.");
    }

    [Fact]
    public void AGoodEveningIsGreenAndAnOrdinaryOneIsNot()
    {
        // The bands have to discriminate, or they are decoration. One goal with the chances to
        // back it is a good evening; the same man doing nothing is not.
        var good = ASnapshotOf(Position.ATT, teamId: ATeam());
        good.PlayedInMatch = true;
        good.MatchGoals = 1;
        for (var shot = 0; shot < 4; shot++)
        {
            good.Performance.RecordShot(onTarget: true, chance: 0.30);
        }

        var ordinary = ASnapshotOf(Position.ATT, teamId: ATeam());
        ordinary.PlayedInMatch = true;
        AnOrdinaryEveningOn(ordinary);

        Assert.Equal(MatchRatingBand.Green, MatchRating.BandOf(MatchRating.Of(good, AFullMatch, AFullMatch)));
        Assert.Equal(MatchRatingBand.Yellow, MatchRating.BandOf(MatchRating.Of(ordinary, AFullMatch, AFullMatch)));
    }

    [Fact]
    public void AGSendingOffIsTheBiggestSingleThingThatCanHappenToAMan()
    {
        // It has to be: a man who is off has spent the rest of the match watching, and a
        // rating that let a hat-trick buy a red card back was measuring goals rather than
        // football.
        var redAndBrace = ASnapshotOf(Position.ATT, teamId: ATeam());
        redAndBrace.PlayedInMatch = true;
        redAndBrace.MatchGoals = 2;
        redAndBrace.MatchYellowCards = 1;
        redAndBrace.RedCard = true;
        redAndBrace.Performance.RecordShot(onTarget: true, chance: 0.4);
        redAndBrace.Performance.RecordShot(onTarget: true, chance: 0.4);

        var yellowAndBrace = ASnapshotOf(Position.ATT, teamId: ATeam());
        yellowAndBrace.PlayedInMatch = true;
        yellowAndBrace.MatchGoals = 2;
        yellowAndBrace.MatchYellowCards = 1;
        yellowAndBrace.Performance.RecordShot(onTarget: true, chance: 0.4);
        yellowAndBrace.Performance.RecordShot(onTarget: true, chance: 0.4);

        Assert.True(
            MatchRating.Of(redAndBrace, AFullMatch, AFullMatch)
            < MatchRating.Of(yellowAndBrace, AFullMatch, AFullMatch));
    }

    [Fact]
    public void AnInjuryIsNotSomethingAManIsMarkedDownFor()
    {
        // A man who goes off hurt at thirty has not played badly, he has been unlucky. Putting
        // it in the number is punishing a body for something it did, and it is the one event
        // in a match where the two things most managers would weigh are not the same thing.
        var hurt = ASnapshotOf(Position.MID, teamId: ATeam());
        hurt.PlayedInMatch = true;
        hurt.InjuredOff = true;
        hurt.Injury = Injury.Grave;
        hurt.InjuryMatchesOut = 3;
        hurt.LeftAtMinute = 30;

        // The same thirty minutes without the injury, so that the only thing the two differ
        // by is the knock.
        var whole = ASnapshotOf(Position.MID, teamId: ATeam());
        whole.PlayedInMatch = true;
        whole.LeftAtMinute = 30;

        Assert.Equal(
            MatchRating.Of(whole, minutesPlayed: 30, totalMinutes: AFullMatch)!.Value,
            MatchRating.Of(hurt, minutesPlayed: 30, totalMinutes: AFullMatch)!.Value,
            1);
    }

    [Fact]
    public void AGoalConcededIsNotSomebodyElsesKeeperFault()
    {
        // A shot that beats a keeper is a goal, and the keeper did his part. Charging him for
        // the shots he did not stop is the same mistake the codebase already refuses when it
        // says an own goal is a defender's error and not a goal of his.
        var keeper = ASnapshotOf(Position.GK, teamId: ATeam());
        keeper.PlayedInMatch = true;
        keeper.MatchSaves = 4;

        var conceded = MatchRating.Of(keeper, AFullMatch, AFullMatch);

        Assert.True(conceded > MatchRules.RatingBaseline);
    }

    // --- How well he did it ------------------------------------------------------

    [Fact]
    public void TheSameGoalIsAClinicFromOneManAndALuckyOneFromAnother()
    {
        // The whole reason the second axis exists. Both men score once, from four shots, and
        // the counts are identical — so anything that reads only the goals says they had the
        // same evening. They did not: one of them finished what he should have missed.
        var clinical = AStrikerWithFourShots(onTargetCount: 3, chance: 0.35);
        var lucky = AStrikerWithFourShots(onTargetCount: 3, chance: 0.80);

        Assert.True(MatchRating.Of(clinical, AFullMatch, AFullMatch)
                    > MatchRating.Of(lucky, AFullMatch, AFullMatch));
    }

    [Fact]
    public void WinningTheDuelsYouShouldHaveLostIsTheBestEveningACentreBackCanHave()
    {
        // The surprise is not a bonus for skill, it is a bonus for beating the number. A
        // defender winning a duel he had a one in five chance of winning is having a
        // remarkable time, and a rating that only counted duels would rank him with a defender
        // who won the same number of duels he was always going to win.
        var overperforming = ADefenderWithSixDuels(chance: 0.20);
        var expected = ADefenderWithSixDuels(chance: 0.50);

        Assert.True(MatchRating.Of(overperforming, AFullMatch, AFullMatch)
                    > MatchRating.Of(expected, AFullMatch, AFullMatch));
    }

    [Fact]
    public void LosingTheDuelsYouShouldHaveWonIsAQuietlyBadEveningAndSaysSo()
    {
        var unlucky = ADefenderWithSixDuels(chance: 0.85, won: false);
        var expected = ADefenderWithSixDuels(chance: 0.50, won: false);

        Assert.True(MatchRating.Of(unlucky, AFullMatch, AFullMatch)
                    < MatchRating.Of(expected, AFullMatch, AFullMatch));
    }

    [Fact]
    public void AManWhoMetEveryOneOfHisChancesHasNotEarnedAMarkForIt()
    {
        // The other half of the rule, and the case that stops it becoming a second opinion
        // about who is good. Six even duels won is the evening a fifty-fifty man is expected
        // to have, so it is rated as the ordinary evening it is rather than as a good one.
        // The mirror image is not the same evening — losing six duels a man had a fifth of
        // expecting is a much bigger miss than winning six he was favoured in — which is why
        // this is stated one way round and not as a symmetry.
        var asPromised = ASnapshotOf(Position.DEF, teamId: ATeam());
        asPromised.PlayedInMatch = true;
        for (var duel = 0; duel < 6; duel++)
        {
            asPromised.Performance.RecordDuel(won: duel % 2 == 0, chance: 0.50);
        }

        // Three won and three lost at an even chance is three more than the six promised, so
        // there is nothing to read. Stated on the deviation rather than on the rating because
        // a duel won is worth something in its own right: comparing two men with different
        // numbers of duels won would be measuring the duels and calling it the surprise.
        Assert.True(Math.Abs(asPromised.Performance.DuelDeviation) < 1e-9,
            "Three won and three lost at an even chance is three more than the six promised.");

        var betterThanPromised = ASnapshotOf(Position.DEF, teamId: ATeam());
        betterThanPromised.PlayedInMatch = true;
        for (var duel = 0; duel < 6; duel++)
        {
            betterThanPromised.Performance.RecordDuel(won: true, chance: 0.20);
        }

        Assert.True(betterThanPromised.Performance.DuelDeviation > 0d);
    }

    [Fact]
    public void ASurprisinglyLongMatchIsCappedAndNotCountedHourAfterHour()
    {
        // The cap, read from what it actually bounds. It is on the surprise axis only, and it
        // has to be: a man who was marginally better than expected in a long afternoon has had
        // a good afternoon, and counting that hour after hour would print a number that says
        // he was flawless. The duels he *won* still count at full weight, because a
        // midfielder with sixty duels won really did have a match, and the cap must not be
        // read as a way of saying that being in the match matters less than being brilliant
        // in it.
        var aFew = ADefenderWithDuels(count: 2, chance: 0.10);
        var aLot = ADefenderWithDuels(count: 60, chance: 0.10);

        var difference = MatchRating.Of(aLot, AFullMatch, AFullMatch)!.Value
                         - MatchRating.Of(aFew, AFullMatch, AFullMatch)!.Value;

        var uncappedSurprise = 58 * 0.90 * MatchRules.RatingDuelSurprise;
        var cappedSurprise = MatchRules.RatingMaxSurprise;

        Assert.True(uncappedSurprise > MatchRules.RatingMaxSurprise,
            "Sixty duels at a tenth each really is more surprise than the cap allows.");
        Assert.True(difference < uncappedSurprise * MatchRules.RatingShrink,
            "The sum was capped rather than added up hour after hour.");
        Assert.True(difference > cappedSurprise * MatchRules.RatingShrink - 0.3,
            "The cap is on the surprise, not on the duels he won.");
    }

    [Fact]
    public void AGivenMatchAlwaysProducesTheSameNumber()
    {
        // The engine is seeded and a replayed match is the same match. A rating that came out
        // differently twice for the same events would be a rating nobody could argue about.
        var first = AStrikerWithFourShots(onTargetCount: 3, chance: 0.35);
        var second = AStrikerWithFourShots(onTargetCount: 3, chance: 0.35);

        Assert.Equal(MatchRating.Of(first, AFullMatch, AFullMatch), MatchRating.Of(second, AFullMatch, AFullMatch));

    }

    // --- Being in the match at all ----------------------------------------------

    [Fact]
    public void AForwardWhoWasInNothingIsMarkedDownForIt()
    {
        // The rule the whole third axis is for. He was on the pitch for ninety minutes and
        // touched nothing: that is not an ordinary evening, and reading it as one would make
        // the rating blind to the complaint every manager makes about a forward who stands
        // in the penalty area waiting for the ball to arrive.
        var present = ASnapshotOf(Position.ATT, teamId: ATeam());
        present.PlayedInMatch = true;

        var ghost = ASnapshotOf(Position.ATT, teamId: ATeam());
        ghost.PlayedInMatch = true;

        Assert.True(MatchRating.Of(ghost, AFullMatch, AFullMatch) < MatchRules.RatingBaseline);
        Assert.Equal(MatchRatingBand.Red, MatchRating.BandOf(MatchRating.Of(ghost, AFullMatch, AFullMatch)));
    }

    [Fact]
    public void AForwardWhoWasInNothingIsNotMarkedOffThePitch()
    {
        // The cap on the penalty. One rule should not be able to produce a four on its own: the
        // number is a reading of a whole match, and a man who did no harm in it has not had a
        // disastrous one.
        var ghost = ASnapshotOf(Position.ATT, teamId: ATeam());
        ghost.PlayedInMatch = true;

        var drop = MatchRules.RatingBaseline - MatchRating.Of(ghost, AFullMatch, AFullMatch)!.Value;

        Assert.True(drop <= MatchRules.RatingMaxInactivityOutfield + 0.01,
            "The inactivity rule is bounded, and a rating that one rule can take to four is not a rating.");
    }

    [Fact]
    public void AGoalkeeperIsAlmostNeverMarkedDownForAQuietEvening()
    {
        // His evening is measured on his saves, and a quiet keeper is a quiet keeper. A rule
        // that read all four of them the same would be saying that a keeper who faced nothing
        // did badly, which is a thing no manager has ever said about a keeper.
        var keeper = ASnapshotOf(Position.GK, teamId: ATeam());
        keeper.PlayedInMatch = true;

        var striker = ASnapshotOf(Position.ATT, teamId: ATeam());
        striker.PlayedInMatch = true;

        Assert.True(MatchRating.Of(keeper, AFullMatch, AFullMatch) > MatchRating.Of(striker, AFullMatch, AFullMatch));
    }

    [Fact]
    public void AQuietCentreBackIsNotTreatedLikeAGhostForward()
    {
        // The one the whole third axis exists for. A centre-back being unmarked is what
        // playing well looks like from the back four, and a flat penalty for it would make the
        // best defenders in the world grade below the worst strikers.
        var defender = ASnapshotOf(Position.DEF, teamId: ATeam());
        defender.PlayedInMatch = true;

        var striker = ASnapshotOf(Position.ATT, teamId: ATeam());
        striker.PlayedInMatch = true;

        var defenderMark = MatchRating.Of(defender, AFullMatch, AFullMatch)!.Value;
        var strikerMark = MatchRating.Of(striker, AFullMatch, AFullMatch)!.Value;

        Assert.True(defenderMark > strikerMark,
            "The same silence is not the same evening for a centre-back and a forward.");
    }

    [Fact]
    public void AManWhoDidMoreThanHisJobAsksIsNeverMarkedDownForHowLittleThereWas()
    {
        // The rule is about a man who was absent, not about a substitute. A fifteen minute
        // cameo with one honest involvement is not a quiet evening.
        var cameo = ASnapshotOf(Position.ATT, teamId: ATeam());
        cameo.PlayedInMatch = true;
        cameo.EnteredAtMinute = 75;
        cameo.Performance.RecordInvolvement();
        cameo.Performance.RecordInvolvement();

        Assert.True(MatchRating.Of(cameo, minutesPlayed: 15, totalMinutes: AFullMatch)
                    >= MatchRules.RatingBaseline);
    }

    [Fact]
    public void TheInactivityPenaltyGrowsWithTheTimeItAppliesTo()
    {
        // Fifteen silent minutes off the bench is not ninety. A rule that ignored the share of
        // the match would dock a man who was on for a quarter of it as hard as one who stood
        // there all afternoon.
        var quarter = ASnapshotOf(Position.ATT, teamId: ATeam());
        quarter.PlayedInMatch = true;
        quarter.EnteredAtMinute = 75;

        var full = ASnapshotOf(Position.ATT, teamId: ATeam());
        full.PlayedInMatch = true;

        Assert.True(MatchRating.Of(quarter, minutesPlayed: 15, totalMinutes: AFullMatch)!.Value
                    > MatchRating.Of(full, AFullMatch, AFullMatch)!.Value);
    }

    // --- The bands ---------------------------------------------------------------

    [Theory]
    [InlineData(null, MatchRatingBand.Unrated)]
    [InlineData(0.0, MatchRatingBand.Red)]
    [InlineData(5.9, MatchRatingBand.Red)]
    [InlineData(6.0, MatchRatingBand.Yellow)]
    [InlineData(7.9, MatchRatingBand.Yellow)]
    [InlineData(8.0, MatchRatingBand.Green)]
    [InlineData(9.9, MatchRatingBand.Green)]
    [InlineData(10.0, MatchRatingBand.Diamond)]
    public void TheBandsAreReadFromTheNumberAndNotDecidedAnywhereElse(double? rating, MatchRatingBand band)
    {
        // A screen that decides where "good" starts is a screen that will decide it
        // differently from the next one, so the whole table is asserted here in one place.
        Assert.Equal(band, MatchRating.BandOf(rating));
    }

    [Fact]
    public void TheCeilingIsTheDiamondAndNotABandAboveIt()
    {
        // There is one way to wear it, and no rating can exceed the scale to reach another.
        Assert.Equal(MatchRatingBand.Diamond, MatchRating.BandOf(11d));
        Assert.Equal(MatchRatingBand.Diamond, MatchRating.BandOf(MatchRules.RatingCeiling));
    }

    [Fact]
    public void NoMatchCanProduceANumberOffTheScale()
    {
        // A hat-trick, a red card and every possible mistake in the same ninety minutes.
        var everything = ASnapshotOf(Position.ATT, teamId: ATeam());
        everything.PlayedInMatch = true;
        everything.MatchGoals = 6;
        everything.MatchOwnGoals = 2;
        everything.MatchYellowCards = 2;
        everything.RedCard = true;
        everything.Performance.RecordDuel(true, 0.0);
        everything.Performance.RecordDuel(true, 0.0);

        var rating = MatchRating.Of(everything, AFullMatch, AFullMatch)!.Value;

        Assert.InRange(rating, MatchRules.RatingFloor, MatchRules.RatingCeiling);
    }

    [Fact]
    public void EveryRatingIsReadToOneDecimalPlace()
    {
        // A card with two decimals on it is a card that is telling you it knows more about a
        // man than a match can say.
        var player = AStrikerWithFourShots(onTargetCount: 3, chance: 0.4127);

        var rating = MatchRating.Of(player, AFullMatch, AFullMatch)!.Value;

        Assert.Equal(rating, Math.Round(rating, 1));
    }

    // --- Helpers -----------------------------------------------------------------

    private static Guid ATeam() => Guid.NewGuid();

    /// <summary>
    /// A quiet but present evening: a man who passed, carried and challenged for ninety
    /// minutes and produced nothing anybody would write down. This is what most of a squad's
    /// cards are, and it is the case the baseline is the answer to.
    /// </summary>
    private static void AnOrdinaryEveningOn(MatchPlayerSnapshot player)
    {
        for (var moment = 0; moment < 3; moment++)
        {
            player.Performance.RecordInvolvement();
        }
    }

    private static double ForGoals(int goals)
    {
        var player = ASnapshotOf(Position.ATT, teamId: ATeam());
        player.PlayedInMatch = true;
        player.MatchGoals = goals;

        for (var shot = 0; shot < goals * 2; shot++)
        {
            player.Performance.RecordShot(onTarget: true, chance: 0.4);
        }

        return MatchRating.Of(player, AFullMatch, AFullMatch)!.Value;
    }

    private static MatchPlayerSnapshot AStrikerWithFourShots(int onTargetCount, double chance)
    {
        var player = ASnapshotOf(Position.ATT, teamId: ATeam());
        player.PlayedInMatch = true;

        for (var shot = 0; shot < 4; shot++)
        {
            player.Performance.RecordShot(onTarget: shot < onTargetCount, chance);
        }

        return player;
    }

    private static MatchPlayerSnapshot ADefenderWithSixDuels(double chance, bool won = true) =>
        ADefenderWithDuels(count: 6, chance, won);

    private static MatchPlayerSnapshot ADefenderWithDuels(int count, double chance, bool won = true)
    {
        var player = ASnapshotOf(Position.DEF, teamId: ATeam());
        player.PlayedInMatch = true;

        for (var duel = 0; duel < count; duel++)
        {
            player.Performance.RecordDuel(won, chance);
        }

        return player;
    }

    private static MatchPlayerSnapshot ASnapshotOf(Position position, Guid teamId)
    {
        var player = Player.Create(
            "Zé da bola", 26, position,
            speed: 60, accuracy: 60, dribbling: 60, heading: 60, strength: 60,
            goalkeeperPower: position == Position.GK ? 65 : 0,
            reflexes: position == Position.GK ? 65 : 0,
            stamina: 60,
            potential: 80);

        var state = PlayerSeasonState.Create(player.Id, Guid.NewGuid(), teamId, energy: 85);

        return MatchPlayerSnapshot.FromPlayerSeasonState(player, state);
    }
}
