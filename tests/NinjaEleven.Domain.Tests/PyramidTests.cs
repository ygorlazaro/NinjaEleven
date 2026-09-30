using NinjaEleven.Domain.Competitions;
using Xunit;

namespace NinjaEleven.Domain.Tests.Competitions;

/// <summary>
/// The pyramid a finished season hands to the next one. It is a closed system of four
/// divisions, so every tier must come out with the same sixteen clubs it started with, and a
/// club that was relegated out of a tier is not in that tier any more.
/// </summary>
public class PyramidTests
{
    private static IReadOnlyDictionary<int, DivisionMovement> ASeason(int clubsPerDivision = 16)
    {
        var standings = new Dictionary<int, DivisionMovement>();

        foreach (var tier in CompetitionRules.Tiers())
        {
            var table = Enumerable.Range(0, clubsPerDivision)
                .Select(_ => new StandingEntry { TeamId = Guid.NewGuid(), Played = 30 })
                .ToList();

            standings[tier] = DivisionMovement.From(tier, table);
        }

        return standings;
    }

    [Fact]
    public void Every_tier_of_the_next_season_has_the_same_number_of_clubs_it_had()
    {
        var movements = ASeason();

        foreach (var tier in CompetitionRules.Tiers())
        {
            Assert.Equal(CompetitionRules.ClubsPerDivision, Pyramid.ClubsOf(tier, movements).Count);
        }
    }

    [Fact]
    public void A_club_is_in_exactly_one_tier_of_the_next_season()
    {
        // The bug this guards: a tier was built from its own list plus whoever arrived, so a
        // relegated club stayed where it was as well as going, and every club that moved was
        // in two divisions at once. Sixteen clubs became twenty-four and the country became
        // eighty-eight, which is a season a cup of sixty-four cannot be drawn from.
        var movements = ASeason();
        var everyClub = CompetitionRules.Tiers()
            .SelectMany(tier => Pyramid.ClubsOf(tier, movements))
            .ToList();

        Assert.Equal(CompetitionRules.TotalClubs, everyClub.Count);
        Assert.Equal(CompetitionRules.TotalClubs, everyClub.Distinct().Count());
    }

    [Fact]
    public void A_champion_goes_up_and_a_relegated_club_goes_down()
    {
        var movements = ASeason();

        var topOfTheSecond = Pyramid.ClubsOf(2, movements);
        var bottomOfTheFirst = Pyramid.ClubsOf(1, movements);

        // The champion of the second division is its own first entry, and it starts the next
        // season in the first division.
        var champion = movements[2].Champion;
        Assert.Contains(champion, topOfTheSecond == null ? [] : bottomOfTheFirst);
        Assert.DoesNotContain(champion, topOfTheSecond);

        // And the club that finished last of the first division is in the second.
        var last = movements[1].Movements
            .OrderBy(movement => movement.Position)
            .Last();

        Assert.Equal(2, last.ToTier);
        Assert.Contains(last.TeamId, topOfTheSecond);
    }

    [Fact]
    public void A_pyramid_that_does_not_add_up_is_said_out_loud()
    {
        // A division that did not play its season, or one whose table came back half sized,
        // has to fail here rather than be drawn: a season of eighty-eight clubs in a country
        // of sixty-four is a thing the cup refuses three competitions later, and by then it
        // is a world nobody can walk.
        var movements = new Dictionary<int, DivisionMovement>
        {
            [1] = DivisionMovement.From(
                1,
                [new StandingEntry { TeamId = Guid.NewGuid() }])
        };

        var error = Assert.Throws<InvalidOperationException>(() => Pyramid.ClubsOf(1, movements));
        Assert.Contains("came out with 0 clubs instead of 16", error.Message);
    }
}
