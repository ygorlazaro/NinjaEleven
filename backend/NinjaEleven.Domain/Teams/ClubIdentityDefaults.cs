using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Teams;

/// <summary>
/// The crest and the two shirts a club that has never been asked to choose any is given.
///
/// <para>
/// It is worked out from the club's own name rather than drawn, so it is the same every time:
/// a world re-seeded gives every club back the badge it had, and a club whose manager has since
/// drawn his own keeps it, because a manager's choice is never overwritten by a default. The
/// hash is FNV-1a over the name, which is stable across processes and machines — a different
/// hash would give the same club a different badge on the next API restart, and a club's badge
/// is the one thing in the game that has to be the same tomorrow.
/// </para>
///
/// <para>
/// The lettering and the figure are drawn in a third colour the club does not otherwise own,
/// picked as black or white against the shield's own field so that it can be read. A red and
/// black club writing itself in white is the ordinary case, and choosing it here rather than
/// asking the club to invent a third colour is what makes every club in a new world legible.
/// </para>
/// </summary>
public static class ClubIdentityDefaults
{
    private static readonly CrestShape[] Shapes = Enum.GetValues<CrestShape>();
    private static readonly CrestFigure[] Figures = Enum.GetValues<CrestFigure>().Where(figure => figure != CrestFigure.None).ToArray();
    private static readonly KitPattern[] Patterns = Enum.GetValues<KitPattern>();

    /// <summary>The crest this club is given before anybody draws one.</summary>
    public static CrestDesign CrestFor(Team team)
    {
        var digest = Hash(team.Name);
        var field = ClubColours.NormaliseOr(team.SecondaryColor, "#1f3c56");
        var ink = ClubColours.InkOn(field);

        var shape = Shapes[digest % Shapes.Length];
        var figure = Figures[(digest / Shapes.Length) % Figures.Length];
        var textPosition = 0.58 + ((digest / (Shapes.Length * Figures.Length)) % 20) / 100d;
        var emblemPosition = 0.28 + ((digest / (Shapes.Length * Figures.Length * 20)) % 20) / 100d;

        return new CrestDesign(
            shape,
            team.PrimaryColor,
            field,
            new CrestText(team.ShortName, ink, textPosition),
            new CrestEmblem(figure, ink, emblemPosition));
    }

    /// <summary>The first shirt this club is given: its own two colours and a cut of its own.</summary>
    public static KitDesign HomeKitFor(Team team)
    {
        var primary = ClubColours.NormaliseOr(team.PrimaryColor, "#3a6ea5");
        var secondary = ClubColours.NormaliseOr(team.SecondaryColor, "#1f3c56");

        return new KitDesign(
            primary,
            secondary,
            Patterns[Hash(team.Name + ":home") % Patterns.Length],
            ClubColours.InkOn(primary));
    }

    /// <summary>
    /// The second shirt: the two colours the other way round and a different cut, so a club that
    /// has never been asked is at least never in the same shirt twice in one match.
    /// </summary>
    public static KitDesign AwayKitFor(Team team)
    {
        var primary = ClubColours.NormaliseOr(team.SecondaryColor, "#1f3c56");
        var secondary = ClubColours.NormaliseOr(team.PrimaryColor, "#3a6ea5");
        var digest = Hash(team.Name + ":away");

        return new KitDesign(
            primary,
            secondary,
            Patterns[digest % Patterns.Length],
            ClubColours.InkOn(primary));
    }

    /// <summary>
    /// FNV-1a, over UTF-16 code units, as an unsigned 32-bit value — the world's own hash,
    /// shared with the sponsor catalog, which deals a company's size out of its name the same
    /// way this deals a crest out of a club's.
    /// </summary>
    private static uint Hash(string value) => StableHash.Of(value);
}
