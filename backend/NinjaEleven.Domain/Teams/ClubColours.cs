namespace NinjaEleven.Domain.Teams;

/// <summary>
/// The three questions about a colour that a club's identity and its two kits have in common:
/// is this string a colour at all, what colour is it exactly, and how far apart are these two.
/// </summary>
/// <remarks>
/// The measurements are the WCAG ones rather than a distance in RGB, because the question the
/// game actually asks about two shirts is a question a spectator asks: can I tell at a glance
/// who is who. RGB distance says a dark blue and a dark green are far apart — they are, on a
/// colour wheel — while on a pitch at forty metres they are two dark shirts. What separates
/// them is how much light each one returns, which is exactly the relative luminance the
/// contrast ratio is built on.
/// </remarks>
public static class ClubColours
{
    /// <summary>Below this contrast two shirts stop being two shirts.</summary>
    public const double ClashThreshold = 1.6;

    /// <summary>Whether a string is a colour this game is willing to draw.</summary>
    public static bool IsColour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = value.Trim().TrimStart('#');

        if (digits.Length is not (3 or 6))
        {
            return false;
        }

        return digits.All(Uri.IsHexDigit);
    }

    /// <summary>
    /// The colour as six lowercase hexadecimal digits with its hash, or a refusal when the
    /// string is not a colour. Refusing rather than substituting is deliberate: a manager who
    /// saved a broken colour and saw a substituted one would be told their choice was saved
    /// when it was not.
    /// </summary>
    public static string Normalise(string? value)
    {
        if (!IsColour(value))
        {
            throw new ArgumentException($"'{value}' is not a colour like #1b3a6b.", nameof(value));
        }

        var digits = value!.Trim().TrimStart('#').ToLowerInvariant();

        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(character => new string(character, 2)));
        }

        return $"#{digits}";
    }

    /// <summary>Normalises when it can, and falls back when it cannot.</summary>
    public static string NormaliseOr(string? value, string fallback) =>
        IsColour(value) ? Normalise(value) : fallback;

    /// <summary>
    /// How much light a colour returns, from zero (black) to one (white).
    /// </summary>
    public static double RelativeLuminance(string? colour)
    {
        var (red, green, blue) = Channels(colour);

        return 0.2126 * Channel(red) + 0.7152 * Channel(green) + 0.0722 * Channel(blue);
    }

    /// <summary>
    /// The WCAG contrast ratio between two colours, from 1 (identical) to 21 (black on white).
    /// </summary>
    public static double Contrast(string? first, string? second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        var lighter = Math.Max(a, b);
        var darker = Math.Min(a, b);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Whether a colour is dark enough that lettering on it should be light. Used by the
    /// screens when the club has asked for no third colour and the two of its own are close
    /// enough to be unreadable against each other.
    /// </summary>
    public static bool IsDark(string? colour) => RelativeLuminance(colour) < 0.35;

    /// <summary>Black or white, whichever reads on this colour.</summary>
    public static string InkOn(string? colour) => IsDark(colour) ? "#ffffff" : "#111111";

    private static double Channel(int value)
    {
        var channel = value / 255d;

        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static (int Red, int Green, int Blue) Channels(string? colour)
    {
        if (!IsColour(colour))
        {
            return (0, 0, 0);
        }

        var digits = colour!.Trim().TrimStart('#');

        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(character => new string(character, 2)));
        }

        return (
            int.Parse(digits[..2], System.Globalization.NumberStyles.HexNumber),
            int.Parse(digits.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
            int.Parse(digits.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
    }
}
