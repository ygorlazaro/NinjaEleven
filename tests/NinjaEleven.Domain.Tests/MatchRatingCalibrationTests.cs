using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// What the scale does to a hundred real matches.
///
/// <para>
/// The unit tests in <see cref="MatchRatingTests"/> hold the shape of a rating down against
/// hand-built evenings, and a hand-built evening is a thing a test can arrange to be as good
/// as it likes. These hold it down against the only evenings there are: matches played out by
/// the engine, with its own action mix, its own chance distributions and its own quiet
/// afternoons. A weight that reads well on a synthetic snapshot and a weight that survives a
/// season are not the same number, and the difference is found here rather than by a manager
/// reading four hundred cards that all say six.
/// </para>
///
/// <para>
/// Every threshold here was read off a hundred played matches rather than chosen and then
/// defended, and the ones worth reading twice are the spread and the ordering. A scale on
/// which a hundred matches produce the same number is not a scale, and a scale on which a man
/// who scores ranks level with a man who does not is not measuring performance.
/// </para>
/// </summary>
public class MatchRatingCalibrationTests
{
    private const int Matches = 100;

    [Fact]
    public void APlayerWhoPlayedIsGivenANumberAndItIsOnTheScale()
    {
        // The floor on the sample and the ceiling on the answer. A rating off the top of the
        // scale is a card that cannot be drawn, and this is also the assertion that catches a
        // match being tallied twice — the counters are on the snapshot, so a second tally
        // would double every man's evening.
        foreach (var card in CardsFrom(Matches))
        {
            if (card.Minutes < MatchRules.RatingMinimumMinutes)
            {
                Assert.Null(card.Rating);
                continue;
            }

            Assert.NotNull(card.Rating);
            Assert.InRange(card.Rating!.Value, MatchRules.RatingFloor, MatchRules.RatingCeiling);
        }
    }

    [Fact]
    public void MostOfASquadIsRatedAndAlmostNobodyIsLeftWithoutANumber()
    {
        // The five-minute floor is a floor on the sample, not a way of declining to rate
        // people: over a hundred matches the men who played are rated and the men who did not
        // are not, and the two are told apart by their minutes rather than by a coin.
        var cards = CardsFrom(Matches);
        var played = cards.Where(card => card.Minutes >= MatchRules.RatingMinimumMinutes).ToList();
        var onTheBench = cards.Count - played.Count;

        Assert.True(played.Count > cards.Count * 0.95,
            $"Only {played.Count} of {cards.Count} men who were on the pitch were rated.");
        Assert.True(onTheBench < cards.Count * 0.05,
            $"{onTheBench} men were left without a rating, which is more than a cameo is.");
    }

    [Fact]
    public void TheScaleIsActuallyUsed()
    {
        // The one that matters most. A rating that answers six to every man who ever played
        // is a rule that never fires, and it would pass every test about an individual match
        // while telling a manager nothing at all about a hundred of them.
        var ratings = RatedFrom(Matches);

        Assert.InRange(ratings.Average(), 5.9, 6.6);
        Assert.True(Percentile(ratings, 0.90) - Percentile(ratings, 0.10) >= 1.2,
            "The middle eighty of a hundred matches has to be told apart from each other.");
    }

    [Fact]
    public void AGoalIsWorthMoreThanNotScoringAndTheScaleSaysSo()
    {
        // The ordering the whole scale exists to produce. Fourteen per cent of men score, so
        // the average of the two groups has to be well apart or the number on the card is not
        // about the football.
        var cards = CardsFrom(Matches);
        var scorers = cards.Where(c => c.Rating is not null && c.Goals > 0).Select(c => c.Rating!.Value).ToList();
        var quiet = cards.Where(c => c.Rating is not null && c.Goals == 0).Select(c => c.Rating!.Value).ToList();

        Assert.True(scorers.Count > 20, "A hundred matches should produce a workable number of goals.");
        Assert.True(scorers.Average() > quiet.Average() + 0.5,
            $"A man who scored averaged {scorers.Average():F2} and a man who did not {quiet.Average():F2}.");
    }

    [Fact]
    public void AGoalkeeperIsJudgedOnHisSavesAndNotOnHowOftenHeWasInvolved()
    {
        // He is involved in about one thing a match and that thing is a save, so a keeper's
        // card has to move with the saves and not with the involvement count every outfielder
        // is being marked on. Read across keepers, a busy one and a quiet one are told apart.
        var keepers = CardsFrom(Matches)
            .Where(card => card.IsKeeper && card.Rating is not null)
            .ToList();

        Assert.True(keepers.Count > 50, "A hundred matches is a hundred keepers.");
        Assert.True(keepers.Max(c => c.Rating!.Value) - keepers.Min(c => c.Rating!.Value) > 0.8,
            "Two goalkeepers had the same evening in every respect that the card can see.");
    }

    [Fact]
    public void AForwardWhoTouchedNothingIsMarkedDownAndACentreBackWhoTouchedNothingIsNot()
    {
        // The rule the third axis is for, checked against matches rather than against a
        // snapshot: the best defenders in the world are the men least visible in a card, and
        // a scale that graded them below the worst strikers would be measuring the wrong
        // thing very visibly.
        var ghosts = CardsFrom(Matches)
            .Where(card => card.Rating is not null && card.Minutes >= 80 && card.Involvements == 0)
            .ToList();

        if (ghosts.Count == 0)
        {
            return;
        }

        var forwards = ghosts.Where(card => card.Position == Position.ATT).ToList();
        var defenders = ghosts.Where(card => card.Position == Position.DEF).ToList();

        if (forwards.Count == 0 || defenders.Count == 0)
        {
            return;
        }

        Assert.True(forwards.Average(c => c.Rating!.Value) < MatchRules.RatingRedBelow,
            "A forward who was on the pitch for ninety minutes and touched nothing is not ordinary.");
        Assert.True(
            defenders.Average(c => c.Rating!.Value) > forwards.Average(c => c.Rating!.Value),
            "The same silence is not the same evening for a centre-back and a forward.");
    }

    [Fact]
    public void TheBandsAreReachableAndTheCeilingIsNotEverybodys()
    {
        // Green has to be a thing a card can say, or it is a colour with nothing behind it,
        // and the diamond has to be rarer than the green or it means nothing either.
        var ratings = RatedFrom(Matches);
        var green = ratings.Count(rating => rating >= MatchRules.RatingGreen);

        Assert.True(green > ratings.Count * 0.005,
            $"{green} of {ratings.Count} men had a good evening, which is too few for the band to exist.");
        Assert.True(ratings.Max() > 8.5,
            "Nothing in a hundred matches was worth the top of the scale.");
        Assert.True(ratings.Count(rating => rating >= MatchRules.RatingCeiling) < ratings.Count * 0.01,
            "The best card on a pitch is not one in a hundred.");
    }

    [Fact]
    public void TheSameMatchAlwaysProducesTheSameCards()
    {
        // The engine is seeded and a replayed match is the same match. A rating that came out
        // differently twice for the same ninety minutes would be a number nobody could argue
        // about, and the history a manager reads would depend on when it was asked for.
        var first = CardsFrom(20);
        var second = CardsFrom(20);

        Assert.Equal(
            first.Select(card => card.Rating),
            second.Select(card => card.Rating));
    }

    // --- The harness ------------------------------------------------------------

    private record Card(
        MatchPlayerSnapshot Player,
        int Minutes,
        int Involvements,
        int Goals,
        bool IsKeeper,
        Position Position,
        double? Rating);

    private static List<Card> CardsFrom(int matches)
    {
        var cards = new List<Card>(matches * 22);

        for (var seed = 1; seed <= matches; seed++)
        {
            var home = new TeamInfo(Guid.NewGuid(), "Home", "HU", "#FF0000", "#FFFFFF", 55);
            var away = new TeamInfo(Guid.NewGuid(), "Away", "AC", "#0000FF", "#FFFFFF", 55);
            var homeEleven = ElevenOf(home);
            var awayEleven = ElevenOf(away);

            var random = new DeterministicRandomSource(seed);
            var context = new MatchContext(
                Guid.NewGuid(), home, away, homeEleven, awayEleven, [], [], random, managerTeamId: null);
            var state = new MatchState(context);
            var engine = new MatchEngine(random);

            engine.Initialize(state, 0).ToList();
            for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
            {
                if (state.HalfTimePauseActive)
                {
                    engine.ContinueSecondHalf(state);
                }

                engine.Tick(state).ToList();
            }

            foreach (var player in homeEleven.Concat(awayEleven))
            {
                var minutes = player.MinutesPlayed(state.Minute);

                cards.Add(new Card(
                    player,
                    minutes,
                    player.Performance.Involvements,
                    player.MatchGoals,
                    player.KeepsGoal,
                    player.Position,
                    MatchRating.Of(player, minutes, state.Minute)));
            }
        }

        return cards;
    }

    private static List<double> RatedFrom(int matches) =>
        CardsFrom(matches).Where(card => card.Rating is not null).Select(card => card.Rating!.Value).ToList();

    private static double Percentile(List<double> values, double p)
    {
        var sorted = values.OrderBy(value => value).ToList();
        return sorted[(int)(p * (sorted.Count - 1))];
    }

    /// <summary>
    /// A conventional eleven of identical, ordinary men. Two squads the same is deliberate:
    /// this is a test of what the scale does to an evening, and a fixture decided by quality
    /// would only add a second thing to read.
    /// </summary>
    private static List<MatchPlayerSnapshot> ElevenOf(TeamInfo team)
    {
        Position[] positions =
        [
            Position.GK, Position.DEF, Position.DEF, Position.DEF, Position.DEF,
            Position.MID, Position.MID, Position.MID, Position.ATT, Position.ATT, Position.ATT
        ];

        var lineup = new List<MatchPlayerSnapshot>();
        for (var i = 0; i < positions.Length; i++)
        {
            var isKeeper = positions[i] == Position.GK;
            var player = Player.Create(
                $"Jogador {i}", 26, positions[i],
                speed: 60, accuracy: 60, dribbling: 60, heading: 60, strength: 60,
                goalkeeperPower: isKeeper ? 60 : 0,
                reflexes: isKeeper ? 60 : 0,
                stamina: 60, potential: 80);

            lineup.Add(MatchPlayerSnapshot.FromPlayerSeasonState(
                player, PlayerSeasonState.Create(player.Id, Guid.NewGuid(), team.Id, energy: 85)));
        }

        return lineup;
    }
}
