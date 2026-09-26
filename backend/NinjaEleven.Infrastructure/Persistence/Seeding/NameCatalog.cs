using System.Reflection;

namespace NinjaEleven.Infrastructure.Persistence.Seeding;

/// <summary>
/// The pool of names and surnames used to generate players. The pools are stored as
/// embedded text resources (one entry per line) and are loaded into the `names` and
/// `surnames` tables by the seeder, so the pool can grow without touching the code that
/// draws from it.
/// </summary>
public static class NameCatalog
{
    private const string FirstNamesResource = "NinjaEleven.Infrastructure.Resources.first-names.txt";
    private const string SurnamesResource = "NinjaEleven.Infrastructure.Resources.surnames.txt";

    private static readonly Lazy<IReadOnlyList<string>> FirstNames = new(() => Load(FirstNamesResource));
    private static readonly Lazy<IReadOnlyList<string>> SurnameEntries = new(() => Load(SurnamesResource));

    public static IReadOnlyList<string> AllFirstNames => FirstNames.Value;
    public static IReadOnlyList<string> AllSurnames => SurnameEntries.Value;

    private static IReadOnlyList<string> Load(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
