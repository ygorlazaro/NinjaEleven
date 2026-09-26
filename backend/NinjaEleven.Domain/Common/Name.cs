using System.Text.Json.Serialization;

namespace NinjaEleven.Domain.Common;

/// <summary>
/// First name ("nome") catalog. The game universe draws player names from this pool,
/// so the same player name can be reused by different players.
/// </summary>
public class Name
{
    public Guid Id { get; private set; }

    [JsonPropertyName("value")]
    public string Value { get; private set; } = string.Empty;

    private Name() { }

    public static Name Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The name cannot be empty.", nameof(value));
        }

        return new Name
        {
            Id = Guid.NewGuid(),
            Value = value.Trim()
        };
    }
}
