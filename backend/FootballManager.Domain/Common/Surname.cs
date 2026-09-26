using System.Text.Json.Serialization;

namespace FootballManager.Domain.Common;

/// <summary>
/// Surname ("sobrenome") catalog. Together with <see cref="Name"/> it forms the
/// full name of a generated player.
/// </summary>
public class Surname
{
    public Guid Id { get; private set; }

    [JsonPropertyName("value")]
    public string Value { get; private set; } = string.Empty;

    private Surname() { }

    public static Surname Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The surname cannot be empty.", nameof(value));
        }

        return new Surname
        {
            Id = Guid.NewGuid(),
            Value = value.Trim()
        };
    }
}
