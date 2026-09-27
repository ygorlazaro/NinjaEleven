namespace NinjaEleven.Application.Models;

/// <summary>
/// How much of a club's recent run a form guide draws.
///
/// This is a reading length, not a rule of football, so it lives with the models rather
/// than in the domain: the domain decides what a result is, and how many of them fit on a
/// card is the card's business. It is one place anyway, because a screen that asked for ten
/// and a service that defaulted to six would be two answers to one question.
/// </summary>
public static class TeamHistoryRules
{
    public const int DefaultHistoryLength = 10;
    public const int MaxHistoryLength = 50;

    public static int Clamp(int requested) =>
        requested < 1 ? DefaultHistoryLength : Math.Min(requested, MaxHistoryLength);
}
