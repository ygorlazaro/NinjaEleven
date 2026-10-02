using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// What a man is worth on a card at the end of a match, worked out from what he did and from
/// what the engine had already said he would do with it.
///
/// <para>
/// Three axes, and they are three because folding them into one number is what makes a
/// rating either unfair or unreadable:
/// </para>
///
/// <list type="number">
/// <item>
/// <b>What he did.</b> Goals, saves, duels, cards. Blunt and absolute, and it is where most
/// of the movement comes from.
/// </item>
/// <item>
/// <b>How well he did it.</b> The engine rolls a probability for every duel and every shot
/// and then throws it away; comparing the roll against that probability is the whole
/// measurement. A striker who scores the chances he should have missed is playing above his
/// profile and one who misses the ones he should have made is playing below it, and neither
/// is visible in a count of goals.
/// </item>
/// <item>
/// <b>Whether he was in the match at all.</b> Measured against what his own job asks of him,
/// so that being quiet is a fact about a centre-back's evening rather than a punishment.
/// </item>
/// </list>
///
/// <para>
/// They do not double count, and that is the reason the second one is surprise rather than
/// "did he meet his own average". A striker's goal is credited once, on the first axis, at
/// full weight, because he is a striker. The second axis never asks how good a man is — the
/// probabilities it reads already contain that — it only asks whether this particular evening
/// went better or worse than those probabilities said it would.
/// </para>
/// </summary>
public static class MatchRating
{
    /// <summary>
    /// The rating for a man, or null when he has not played enough of the match to have one.
    /// </summary>
    /// <param name="player">
    /// The man. His goals, saves and cards are read off him rather than off the tally,
    /// because they are already counted there and counting them again would be a second
    /// number that could disagree with the first.
    /// </param>
    /// <param name="minutesPlayed">How long he was on the pitch for.</param>
    /// <param name="totalMinutes">
    /// How long the match was, so that a rating can be read against a full ninety and not
    /// against a number that happens to be this evening's.
    /// </param>
    public static double? Of(MatchPlayerSnapshot player, int minutesPlayed, int totalMinutes)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (minutesPlayed < MatchRules.RatingMinimumMinutes)
        {
            return null;
        }

        var performance = player.Performance;
        var raw = MatchRules.RatingBaseline
                  + WhatHeDid(player, performance)
                  + HowWellHeDidIt(performance)
                  + BeingAbsentFromIt(player, performance, minutesPlayed, totalMinutes);

        // Shrunk back towards the baseline, because a small sample of a large number of
        // moments should not print a number that says a man was flawless.
        var settled = MatchRules.RatingBaseline + (raw - MatchRules.RatingBaseline) * MatchRules.RatingShrink;
        var bounded = Math.Clamp(settled, MatchRules.RatingFloor, MatchRules.RatingCeiling);

        return Math.Round(bounded, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Which band a number falls in, which is the only thing a screen is allowed to decide for
    /// itself about a rating: the number arrives, the colour follows it, and the client never
    /// works out where the lines were.
    /// </summary>
    public static MatchRatingBand BandOf(double? rating)
    {
        if (rating is not { } value)
        {
            return MatchRatingBand.Unrated;
        }

        if (value >= MatchRules.RatingDiamond)
        {
            return MatchRatingBand.Diamond;
        }

        if (value < MatchRules.RatingRedBelow)
        {
            return MatchRatingBand.Red;
        }

        // Green starts where a good evening starts rather than where a great one does, so the
        // band is worth reaching and 10 stays the only blue diamond on a pitch.
        return value >= MatchRules.RatingGreen
            ? MatchRatingBand.Green
            : MatchRatingBand.Yellow;
    }

    private static double WhatHeDid(MatchPlayerSnapshot player, MatchPerformance performance) =>
        MatchRules.RatingGoal * player.MatchGoals
        + MatchRules.RatingAssist * performance.Assists
        + MatchRules.RatingOwnGoal * player.MatchOwnGoals
        + MatchRules.RatingSave * player.MatchSaves
        + MatchRules.RatingShotOnTarget * performance.ShotsOnTarget
        + MatchRules.RatingShotOffTarget * performance.ShotsOffTarget
        + MatchRules.RatingDuelWon * performance.DuelsWon
        + MatchRules.RatingDuelLost * performance.DuelsLost
        + MatchRules.RatingFoul * performance.FoulsCommitted
        + MatchRules.RatingCorner * performance.CornersWon
        + MatchRules.RatingYellowCard * player.MatchYellowCards
        + (player.RedCard ? MatchRules.RatingRedCard : 0d);

    /// <summary>
    /// The sum of how far every graded moment fell from its own chance, each kind of moment
    /// carrying its own weight, capped.
    /// </summary>
    /// <remarks>
    /// The weights are applied here rather than where the moments are counted, because a duel
    /// and a goal are different sizes of moment and the size of a goal is a question about
    /// this scale. The cap is what keeps a long match of small surprises from outvoting a
    /// goal: a man who was marginally better than expected in fifteen duels has had a good
    /// evening, not a match-winning one, and the difference between those two is the cap.
    /// </remarks>
    private static double HowWellHeDidIt(MatchPerformance performance)
    {
        var weighted = performance.DuelDeviation * MatchRules.RatingDuelSurprise
                       + performance.ShotDeviation * MatchRules.RatingShotSurprise
                       + performance.PenaltyDeviation * MatchRules.RatingPenaltySurprise;

        return Math.Clamp(weighted, -MatchRules.RatingMaxSurprise, MatchRules.RatingMaxSurprise);
    }

    /// <summary>
    /// What a man loses for being on the pitch and in nothing at all.
    /// </summary>
    /// <remarks>
    /// Measured as a shortfall against a floor his own role sets — a midfielder involved in
    /// fewer than two things in ninety minutes was not in the match — and squared, so a man one
    /// moment below the floor is barely marked and a man involved in nothing is marked for
    /// what it is.
    /// </remarks>
    /// <para>
    /// It was a shortfall against what the role was expected to average, and that was measured
    /// over a hundred real matches and it did not work. A squad's involvement is lumpy: a
    /// striker's median evening is three moments and his worst is none, and asking for the
    /// average marked the median player down for being ordinary. Worse, the bands then put the
    /// bottom of the red at exactly the baseline, so a tenth of a mark lost was the whole
    /// difference between an ordinary evening and a bad one, and a rule that fine could not be
    /// gentle anywhere. The ordinary mark has since moved up a point, so the red line now sits
    /// below it rather than on it — which is the same point stated as a rule and not
    /// inherited from whatever the baseline happened to be. A floor is the shape the complaint
    /// actually has: a manager is not
    /// upset that his striker was involved three times, he is upset that a man was involved
    /// none.
    /// </para>
    /// <para>
    /// The cap is read from his own role, and that is the whole reason there are three of them.
    /// A forward who is on the pitch for ninety minutes and touches nothing is a real problem
    /// and is marked as one; a centre-back in the same evening is doing the job, which is to
    /// be unmarked. A single cap would make those two the same number, and the first thing
    /// anyone would say about the result is that the best defenders in the world grade below
    /// the worst strikers.
    /// </para>
    private static double BeingAbsentFromIt(
        MatchPlayerSnapshot player,
        MatchPerformance performance,
        int minutesPlayed,
        int totalMinutes)
    {
        var floorPerNinety = InvolvementFloorFor(player);
        if (floorPerNinety <= 0d)
        {
            return 0d;
        }

        var shareOfAMatch = (double)minutesPlayed / Math.Max(1, totalMinutes);
        var floor = floorPerNinety * shareOfAMatch;

        // A man who cleared his own floor is never docked for how little there was: the rule
        // is about a man who was absent, not about a substitute.
        if (performance.Involvements >= floor)
        {
            return 0d;
        }

        var shortfall = Math.Clamp(1d - performance.Involvements / floor, 0d, 1d);
        var penalty = MatchRules.RatingInactivityWeight * shortfall * shortfall * shareOfAMatch;

        return -Math.Min(penalty, InactivityCapFor(player));
    }

    /// <summary>
    /// How few moments a man can be involved in before his card starts to say he was not
    /// playing, and zero for a goalkeeper because he is measured on his saves: a keeper is
    /// involved in roughly one thing a match and that thing is a save, which the save weight
    /// already reads. Asking a second rule about it would count the same evening twice and
    /// then complain about it.
    /// </summary>
    private static double InvolvementFloorFor(MatchPlayerSnapshot player) =>
        player.KeepsGoal
            ? 0d
            : player.Position == Position.DEF
                ? MatchRules.RatingInvolvementsPerNinetyDefense
                : player.Position == Position.MID
                    ? MatchRules.RatingInvolvementsPerNinetyMidfield
                    : MatchRules.RatingInvolvementsPerNinetyAttack;

    private static double InactivityCapFor(MatchPlayerSnapshot player) =>
        player.KeepsGoal
            ? MatchRules.RatingMaxInactivityGoalkeeper
            : player.Position == Position.DEF
                ? MatchRules.RatingMaxInactivityDefense
                : MatchRules.RatingMaxInactivityOutfield;
}
