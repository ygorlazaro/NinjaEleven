namespace NinjaEleven.Domain.Common;

/// <summary>
/// Season numbers written as roman numerals.
///
/// A season is identified by an integer and shown as a numeral: "Temporada I" is a name, not
/// an identity, and a year is a date rather than a season. Converting the number is a
/// presentation rule that both the backend (which names the season) and any client that
/// displays it have to agree on, so it lives here and nowhere else.
/// </summary>
public static class RomanNumeral
{
    private static readonly (int Value, string Numeral)[] Table =
    {
        (1000, "M"),
        (900, "CM"),
        (500, "D"),
        (400, "CD"),
        (100, "C"),
        (90, "XC"),
        (50, "L"),
        (40, "XL"),
        (10, "X"),
        (9, "IX"),
        (5, "V"),
        (4, "IV"),
        (1, "I")
    };

    /// <summary>
    /// The numeral for a season number. Seasons are counted from one, so a number below one
    /// is a mistake rather than something to render as an empty string.
    /// </summary>
    public static string From(int number)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                number,
                "A season is counted from one; there is no season before the first.");
        }

        if (number > 3999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                number,
                "Roman numerals stop at 3999; a league would be over before that.");
        }

        var remaining = number;
        var numeral = string.Empty;

        foreach (var (value, symbol) in Table)
        {
            while (remaining >= value)
            {
                numeral += symbol;
                remaining -= value;
            }
        }

        return numeral;
    }

    /// <summary>The name a season is shown under.</summary>
    public static string SeasonName(int number) => $"Temporada {From(number)}";
}
