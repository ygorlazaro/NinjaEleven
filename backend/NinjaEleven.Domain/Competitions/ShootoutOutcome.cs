namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// A shootout that has been taken, as the cup needs to remember it.
///
/// It is written from the shootout the match itself played — the one the two managers named
/// their takers for and the crowd watched — and it is keyed by club rather than by side,
/// because the two legs of a tie swap ends: the club that was at home in the first leg is
/// away in the second one, so "home" means one club in one leg and the other in the other, and
/// a shootout counted by side would give a club the penalties its opponent took.
///
/// This replaces the one-shot draw the cup used to settle a level tie with. That draw had the
/// right shape and none of the substance: it took both sides' kicks in a loop, nobody saw a
/// single one of them, and the eleven that played the tie never chose who went to the spot.
/// </summary>
/// <param name="HomeGoals">Penalties the tie's home club scored.</param>
/// <param name="AwayGoals">Penalties the tie's away club scored.</param>
/// <param name="WinnerTeamId">The club that goes through. A shootout is never a draw.</param>
/// <param name="IsSuddenDeath">Whether it had to go past the five kicks each side is given.</param>
public sealed record ShootoutOutcome(
    int HomeGoals,
    int AwayGoals,
    Guid WinnerTeamId,
    bool IsSuddenDeath);
