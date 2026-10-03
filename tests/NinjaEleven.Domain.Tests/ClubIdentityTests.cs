using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// A club's badge, its two shirts, and the draw that decides which of them is worn.
///
/// Everything here is a question a spectator asks and the game has to answer without asking:
/// can I tell these two shirts apart, and is this club's badge still the badge it was
/// yesterday. The parts that are checked are the parts that are stored — a design that does not
/// survive the round trip is a design a manager drew and then lost, and a default that changes
/// between two runs is a club whose badge moves while nobody is looking.
/// </summary>
public class ClubIdentityTests
{
    /// <summary>A dark blue shirt, white trim: the case the rule was written for.</summary>
    private static KitDesign DarkBlue() =>
        new("#123a8f", "#ffffff", KitPattern.Solid, "#ffffff");

    /// <summary>A dark green shirt, yellow trim.</summary>
    private static KitDesign DarkGreen() =>
        new("#0d5c2e", "#ffd700", KitPattern.Solid, "#ffd700");

    [Fact]
    public void ACrestSurvivesTheRoundTripThroughItsOwnStorage()
    {
        var crest = new CrestDesign(
            CrestShape.EaredShield,
            "#B11226",
            "#1F1F1F",
            new CrestText("RBE", "#ffffff", 0.62),
            new CrestEmblem(CrestFigure.Bolt, "#f2d34f", 0.31));

        var read = CrestDesign.Parse(crest.ToJson());

        Assert.NotNull(read);
        Assert.Equal(CrestShape.EaredShield, read!.Shape);
        Assert.Equal("#b11226", read.PrimaryColor);
        Assert.Equal("#1f1f1f", read.SecondaryColor);
        Assert.Equal("RBE", read.Text!.Content);
        Assert.Equal("#ffffff", read.Text.Color);
        Assert.Equal(0.62, read.Text.VerticalPosition, 3);
        Assert.Equal(CrestFigure.Bolt, read.Emblem!.Kind);
        Assert.Equal(0.31, read.Emblem.VerticalPosition, 3);
    }

    [Fact]
    public void AShieldNobodyDrewIsTheInitialsPlaceholderRatherThanABlankShape()
    {
        // A club that has no crest is a club whose screen draws its initials, which is what it
        // did before crests existed. Garbage in the column has to land there too rather than
        // taking a table of sixty-four clubs down.
        Assert.Null(CrestDesign.Parse(null));
        Assert.Null(CrestDesign.Parse("   "));
        Assert.Null(CrestDesign.Parse("{\"shape\":\"NotAShape\"}"));
        Assert.Null(CrestDesign.Parse("not json at all"));
        Assert.Null(CrestDesign.Parse("{\"primaryColor\":\"not-a-colour\"}"));
    }

    [Fact]
    public void TwoDarkShirtsAreOneDarkMass()
    {
        // Blue and green are a long way apart on a colour wheel and almost on top of each other
        // on a pitch. What separates two shirts is how much light each one gives back, so the
        // rule measures that and not the distance between the hues.
        Assert.True(KitClash.AreIndistinguishable(DarkBlue(), DarkGreen()));
    }

    [Fact]
    public void TwoShirtsThatAreAlreadyApartAreLeftExactlyAsTheyWere()
    {
        var white = new KitDesign("#ffffff", "#111111", KitPattern.Solid);
        var red = new KitDesign("#c8102e", "#ffffff", KitPattern.VerticalStripe);

        Assert.False(KitClash.AreIndistinguishable(white, red));
        Assert.Equal((KitSide.Home, KitSide.Home), KitClash.Decide(white, null, red, null, seed: 7));
    }

    [Fact]
    public void WhenTwoShirtsClashOneOfTheTwoClubsChangesAndItIsOneThatFixesIt()
    {
        // The game draws this rather than always sending the visitor out in its second shirt,
        // because either club may be the one holding a shirt that cannot be told from the
        // other's — and because the answer has to be drawn from the fixture rather than
        // settled by a convention that is wrong whenever the visitor is the one whose second
        // shirt is the problem too.
        var green = new KitDesign("#ffd700", "#0d5c2e", KitPattern.HorizontalStripe);
        var (home, away) = KitClash.Decide(DarkBlue(), null, DarkGreen(), green, seed: 11);

        Assert.True(home is KitSide.Away || away is KitSide.Away);
        Assert.False(KitClash.AreIndistinguishable(
            home is KitSide.Away ? new KitDesign("#ffffff", "#123a8f", KitPattern.HorizontalStripe) : DarkBlue(),
            away is KitSide.Away ? green : DarkGreen()));
    }

    [Fact]
    public void AVisitorWithNoSecondShirtCannotBeTheOneWhoChanges()
    {
        // The host has nothing to change into either, so there is nothing to choose and both
        // clubs keep the shirts they turned out in.
        var decision = KitClash.Decide(DarkBlue(), null, DarkGreen(), null, seed: 3);

        Assert.Equal((KitSide.Home, KitSide.Home), decision);
    }

    [Fact]
    public void AVisitorWhoseSecondShirtIsJustAsDarkChangesNothingRatherThanChangingAtRandom()
    {
        // A coin between two unreadable shirts decides nothing, and a rule that changed a shirt
        // at random would leave two dark shirts on the pitch as often as it fixed them.
        var decision = KitClash.Decide(DarkBlue(), DarkBlue(), DarkGreen(), DarkGreen(), seed: 5);

        Assert.Equal((KitSide.Home, KitSide.Home), decision);
    }

    [Fact]
    public void TheDrawBelongsToTheMatchAndNotToTheMomentItIsAsked()
    {
        // The same seed is the same answer every time, which is what makes an abandoned match
        // replayed in the same two shirts.
        var first = KitClash.Decide(DarkBlue(), null, DarkGreen(), DarkGreen(), seed: 99);
        var second = KitClash.Decide(DarkBlue(), null, DarkGreen(), DarkGreen(), seed: 99);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheDrawIsOverTheAnswersThatWorkAndNotOverAllFourOfThem()
    {
        // A green away shirt for a green home shirt is not a fix, so a host with nothing to
        // change into is left where it is rather than a coin deciding which of two unreadable
        // pairs is slightly less unreadable.
        var greenAway = new KitDesign("#14532d", "#ffeb3b", KitPattern.ThinStripes);

        Assert.True(KitClash.AreIndistinguishable(DarkGreen(), greenAway));
        Assert.Equal(
            (KitSide.Home, KitSide.Home),
            KitClash.Decide(DarkBlue(), null, DarkGreen(), greenAway, seed: 42));

        // A second shirt that does separate the two is used, and every seed reaches the same
        // answer because it is the only one on the list.
        var whiteAway = new KitDesign("#ffd700", "#0d5c2e", KitPattern.ThinStripes);

        Assert.False(KitClash.AreIndistinguishable(DarkGreen(), whiteAway));

        for (var seed = 0; seed < 16; seed++)
        {
            Assert.Equal(
                (KitSide.Home, KitSide.Away),
                KitClash.Decide(DarkBlue(), null, DarkGreen(), whiteAway, seed));
        }
    }

    [Fact]
    public void AHostThatIsTheOneHoldingTheImpossibleShirtIsAllowedToChange()
    {
        // The visitor's second shirt is as dark as its first, so the only answers left are the
        // host's two shirts — and the draw is between them rather than fixed to either.
        var yellowHome = new KitDesign("#ffd700", "#123a8f", KitPattern.HorizontalStripe);
        var greenAway = new KitDesign("#14532d", "#ffeb3b", KitPattern.ThinStripes);

        var answers = Enumerable.Range(0, 16)
            .Select(seed => KitClash.Decide(DarkBlue(), yellowHome, DarkGreen(), greenAway, seed))
            .Distinct()
            .ToList();

        Assert.All(answers, answer => Assert.Equal(KitSide.Away, answer.Home));
        Assert.Contains((KitSide.Away, KitSide.Home), answers);
        Assert.Contains((KitSide.Away, KitSide.Away), answers);
    }

    [Fact]
    public void AClubThatHasNeverBeenDrawnStillHasTwoDifferentShirts()
    {
        var club = Team.Create("Rio Branco Esporte Clube", "RBE", "#B11226", "#1F1F1F");

        Assert.NotEqual(club.KitInUse(KitSide.Home).PrimaryColor, club.KitInUse(KitSide.Away).PrimaryColor);
    }

    [Fact]
    public void AShirtWithoutAChosenNumberIsReadInWhateverColourCanBeRead()
    {
        var dark = new KitDesign("#123a8f", "#ffffff", KitPattern.Solid);
        var light = new KitDesign("#ffffff", "#123a8f", KitPattern.Solid);

        Assert.Equal("#ffffff", dark.NumberColor);
        Assert.NotEqual("#ffffff", light.NumberColor);
    }

    [Fact]
    public void EveryClubIsGivenTheSameBadgeEveryTimeItIsAsked()
    {
        var club = Team.Create("Ferroviário Atlético", "FER", "#1B3A6B", "#D4AF37");

        var first = ClubIdentityDefaults.CrestFor(club);
        var second = ClubIdentityDefaults.CrestFor(club);

        Assert.Equal(first.Shape, second.Shape);
        Assert.Equal(first.Text!.Content, second.Text!.Content);
        Assert.Equal(first.Text.Color, second.Text.Color);
        Assert.Equal(first.Text.VerticalPosition, second.Text.VerticalPosition, 6);
        Assert.Equal(first.Emblem!.Kind, second.Emblem.Kind);
        Assert.Equal(first.Emblem.VerticalPosition, second.Emblem.VerticalPosition, 6);
    }

    [Fact]
    public void AnElementDraggedToEitherEndStillLandsInsideTheShield()
    {
        // The position is the middle of the element, so a badge dragged to the very top would
        // otherwise hang half its name and half its figure out of the shield. The ends of the
        // two ranges are where the tallest figure and the longest line of lettering still fit
        // inside the narrowest of the ten shapes.
        Assert.Equal(CrestText.LowestPosition, new CrestText("RBE", "#ffffff", -5).VerticalPosition);
        Assert.Equal(CrestText.HighestPosition, new CrestText("RBE", "#ffffff", 5).VerticalPosition);
        Assert.Equal(CrestEmblem.LowestPosition, new CrestEmblem(CrestFigure.Bolt, "#ffffff", 0).VerticalPosition);
        Assert.Equal(CrestEmblem.HighestPosition, new CrestEmblem(CrestFigure.Bolt, "#ffffff", 1).VerticalPosition);
    }

    [Fact]
    public void ADefaultBadgeAlwaysSaysSomethingAndAlwaysCanBeRead()
    {
        // A red and black club writing itself in white is the ordinary case, and it is why the
        // default picks the lettering rather than asking every club to invent a third colour.
        var club = Team.Create("Rio Pardo Futebol Clube", "RPF", "#C8102E", "#000000");
        var crest = ClubIdentityDefaults.CrestFor(club);

        Assert.True(crest.HasElement);
        Assert.Equal("#ffffff", crest.Text!.Color);
        Assert.True(ClubColours.Contrast(crest.Text.Color, crest.SecondaryColor) >= 3);
    }

    [Fact]
    public void ADrawnCrestSurvivesOnTheClubAndIsReadBackFromIt()
    {
        var club = Team.Create("Estrela do Norte", "EDN", "#0F5132", "#FFD700");
        club.SetCrest(new CrestDesign(CrestShape.Diamond, "#0F5132", "#FFD700", new CrestText("EDN", "#ffffff", 0.5)));
        club.SetHomeKit(new KitDesign("#0F5132", "#FFD700", KitPattern.Checkered));

        Assert.Equal(CrestShape.Diamond, club.Crest!.Shape);
        Assert.Equal(KitPattern.Checkered, club.HomeKit!.Pattern);
        Assert.Equal("EDN", club.Crest.Text!.Content);

        // And a club that clears its badge is a club whose shield is the placeholder again,
        // which is what a club that never had one has always been.
        club.SetCrest(null);
        Assert.Null(club.Crest);
    }

    [Fact]
    public void AMatchRemembersTheTwoShirtsItWasPlayedInRatherThanDrawingThemAgain()
    {
        // The answer belongs to the fixture: a manager who reloads his own match at minute
        // seventy has to be looking at the same two shirts he was looking at before, and a
        // screen that redrew the decision on every read would change a visitor's colours under
        // him in the middle of the second half.
        var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(KitSide.Home, match.HomeKitSide);
        Assert.Equal(KitSide.Home, match.AwayKitSide);

        match.RecordKits(KitSide.Home, KitSide.Away);

        Assert.Equal(KitSide.Home, match.HomeKitSide);
        Assert.Equal(KitSide.Away, match.AwayKitSide);
    }

    [Fact]
    public void AMatchRemembersTheTwoCompaniesOnTheShirtsRatherThanAskingTheContractsAgain()
    {
        // Same reasoning as the two shirts, and it matters more: a deal runs out in the middle of
        // a season, so a scoreboard that asked the contract table on every draw would take the
        // company off a club's back at half-time and put it back at the final whistle. The two
        // names are what the referee saw when the whistle went.
        var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();

        Assert.Null(match.HomeSponsorId);
        Assert.Null(match.AwaySponsorId);

        match.RecordSponsors(home, away);

        Assert.Equal(home, match.HomeSponsorId);
        Assert.Equal(away, match.AwaySponsorId);
    }

    [Fact]
    public void AClubWithNoSponsorLeavesAHalfOfTheScoreboardEmptyRatherThanNamingSomebody()
    {
        // Most clubs have no deal for most of a season, and a null is that fact. Substituting a
        // name for the gap would put a company on a shirt that was sold to somebody else.
        var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        match.RecordSponsors(Guid.NewGuid(), null);

        Assert.NotNull(match.HomeSponsorId);
        Assert.Null(match.AwaySponsorId);
    }
}
