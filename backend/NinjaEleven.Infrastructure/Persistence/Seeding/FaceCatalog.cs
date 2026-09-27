using System.Reflection;
using System.Text.Json;

namespace NinjaEleven.Infrastructure.Persistence.Seeding;

/// <summary>
/// The pool of faces the seeder hands out to players.
///
/// A face is a faces.js <c>FaceConfig</c> — a small JSON object the frontend draws — so the
/// pool is an embedded resource of faces rather than a table of its own: the faces are
/// identical for everybody and only the choice of one is per player, which is exactly what
/// the <c>players.face</c> column stores. The pool is drawn by
/// <c>frontend/ninja-eleven-web/scripts/generate-face-pool.mjs</c> (<c>npm run
/// faces:generate</c>), because the library that draws a face is a JavaScript one.
/// </summary>
public static class FaceCatalog
{
    private const string FacesResource = "NinjaEleven.Infrastructure.Resources.faces.json";

    private static readonly Lazy<IReadOnlyList<string>> Faces = new(Load);

    /// <summary>
    /// Every face in the pool, as the JSON that goes straight into the column. The pool is
    /// larger than a squad, so a caller can deal from it without replacement: two players in
    /// the same club should not share a nose.
    /// </summary>
    public static IReadOnlyList<string> All => Faces.Value;

    private static IReadOnlyList<string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FacesResource)
            ?? throw new InvalidOperationException($"Embedded resource '{FacesResource}' was not found.");

        using var document = JsonDocument.Parse(stream);

        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{FacesResource}' must be a non-empty array of faces.");
        }

        return document.RootElement
            .EnumerateArray()
            .Select(face => face.GetRawText())
            .ToList();
    }
}
