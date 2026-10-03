using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Sponsors;
using NinjaEleven.Domain.Teams;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The mark a company is drawn as.
///
/// <para>
/// A sponsor's logo is worked out rather than stored, which is only worth doing if the same
/// company comes out the same way every time it is drawn — on a scoreboard, in the sponsor book
/// and on a transfer card. So the first thing asserted here is that it is a function of the name
/// and the brand colour and nothing else, and the second is that what it produces is a mark a
/// screen can draw: a panel, an ink and a name short enough to fit inside it.
/// </para>
/// </summary>
public class SponsorLogoTests
{
    [Fact]
    public void A_company_keeps_the_same_mark_on_every_draw()
    {
        // The same company is drawn in three places of the game and they have to agree, or a
        // manager signs a deal with the mark on the shirt and sees a different one in the book.
        var first = SponsorLogoDefaults.LogoFor("Metalúrgica do Vale", "#c0392b");
        var second = SponsorLogoDefaults.LogoFor("Metalúrgica do Vale", "#c0392b");

        Assert.Equal(first.Shape, second.Shape);
        Assert.Equal(first.BackgroundColor, second.BackgroundColor);
        Assert.Equal(first.InkColor, second.InkColor);
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public void A_brand_colour_decides_the_panel_and_not_the_name()
    {
        // Two companies with the same name in two divisions are the same brand, and a brand does
        // not get two marks because a seeder wrote the row twice.
        var first = SponsorLogoDefaults.LogoFor("Banco do Litoral", "#1f6f3f");
        var second = SponsorLogoDefaults.LogoFor("Banco do Litoral", "#1f6f3f");

        Assert.Equal(first.BackgroundColor, second.BackgroundColor);
    }

    [Fact]
    public void The_panel_is_the_brand_colour_the_club_signed_up_for()
    {
        // The colour on the offer is the colour the manager chose between, so the mark that goes
        // on the shirt has to be drawn in it.
        var logo = SponsorLogoDefaults.LogoFor("Transportes Ipiranga", "#14487f");

        Assert.Equal("#14487f", logo.BackgroundColor);
    }

    [Fact]
    public void The_lettering_is_black_or_white_because_it_has_to_be_read()
    {
        // An ink picked from the same palette as the panel is a mark nobody can read at twenty
        // pixels in a table, and a mark nobody can read is not on a shirt.
        var onDark = SponsorLogoDefaults.LogoFor("Metalúrgica do Vale", "#101820");
        var onLight = SponsorLogoDefaults.LogoFor("Metalúrgica do Vale", "#f7f3e8");

        Assert.Equal(ClubColours.InkOn("#101820"), onDark.InkColor);
        Assert.Equal(ClubColours.InkOn("#f7f3e8"), onLight.InkColor);
        Assert.NotEqual(onDark.InkColor, onLight.InkColor);
    }

    [Fact]
    public void A_name_too_long_for_a_shirt_is_cut_rather_than_shrunk_to_nothing()
    {
        // Every panel in the game is about three times as wide as it is tall, so a name of any
        // length has to end up inside it. What the mark may not do is write the company's whole
        // legal name out at a font nobody can read.
        var logo = SponsorLogoDefaults.LogoFor(
            "Companhia de Navegação e Comércio do litoral brasileiro",
            "#8e44ad");

        Assert.NotEmpty(logo.Text);
        Assert.True(
            logo.Text.Length <= SponsorLogoDesign.MaxLength,
            $"the mark carried {logo.Text.Length} characters, over the {SponsorLogoDesign.MaxLength} allowed.");
    }

    [Fact]
    public void A_short_name_is_written_out_in_full()
    {
        // The cut above is a rule for the long ones, not a licence to abbreviate a company whose
        // name fits.
        var logo = SponsorLogoDefaults.LogoFor("Itau", "#ec7000");

        Assert.Equal("Itau", logo.Text);
    }

    [Fact]
    public void Every_panel_the_game_deals_is_one_the_client_can_draw()
    {
        // The eight panels are a contract with the client. A ninth dealt out here would be a
        // badge the screen has no drawing for, and a shirt nobody in the game has ever seen.
        var panels = Enumerable
            .Range(0, 64)
            .Select(seed => SponsorLogoDefaults.LogoFor($"Empresa {seed}", "#345678").Shape)
            .Distinct()
            .ToList();

        Assert.All(panels, shape => Assert.True(
            Enum.IsDefined(shape),
            $"the mark dealt a panel the game does not know: {shape}."));
    }
}