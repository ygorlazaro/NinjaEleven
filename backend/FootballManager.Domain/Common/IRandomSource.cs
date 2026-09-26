namespace FootballManager.Domain.Common;

/// <summary>
/// Abstraction over randomness so the Match Engine can be tested deterministically.
/// The domain never calls System.Random directly.
/// </summary>
public interface IRandomSource
{
    int Next(int min, int max);
    double NextDouble();
}