using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Matches;
using Match = NinjaEleven.Domain.Matches.Match;

namespace NinjaEleven.Application.Mappings;

/// <summary>
/// Builds what the cup is told about a leg that has just finished.
///
/// It is its own type because this is the seam where a match stops being a match and
/// becomes a fact about a tie: everything the cup is allowed to know about a leg comes out
/// of here, and nothing the cup is given can be used to re-decide the game it came from.
///
/// The shootout is read, never drawn. It was taken by the eleven that finished the leg, in
/// the order the two managers named, and a cup that settled a level tie by a rule of its
/// own would be a cup whose penalties nobody in the stadium had watched — and whose winner
/// was not decided by the players who played the tie. So this reads the answer off the
/// match's own state and hands it over as it stands.
/// </summary>
public static class CupLegOutcomeFactory
{
    /// <summary>
    /// What the cup needs to hear about a finished match, read off the match's own state.
    /// </summary>
    /// <param name="match">The finished match row, which carries the fixture and the seed.</param>
    /// <param name="state">The state the match finished in, shootout included.</param>
    public static CupLegOutcome From(Match match, MatchState state) => new()
    {
        FixtureId = match.FixtureId,
        HomeTeamId = state.HomeTeam.Id,
        AwayTeamId = state.AwayTeam.Id,
        HomeScore = state.HomeScore,
        AwayScore = state.AwayScore,
        Seed = match.Seed,
        Shootout = ShootoutFrom(state)
    };

    /// <summary>
    /// The shootout the leg was settled by, counted by this leg's sides.
    ///
    /// It is null unless the shootout is over, because a shootout that is still being taken
    /// has no result to report — and a leg whose ninety minutes were level and whose
    /// shootout has not finished is a match that has not been decided at all, which the cup
    /// is told about by the leg simply not arriving.
    ///
    /// The sides are this leg's and not the tie's clubs, because that is what a leg's own
    /// numbers are. The two legs swap ends, and the service that reads this back is the one
    /// that knows the swap.
    /// </summary>
    private static ShootoutOutcome? ShootoutFrom(MatchState state) =>
        state.Shootout is { IsComplete: true } taken
            ? new ShootoutOutcome(
                HomeGoals: taken.HomeGoals,
                AwayGoals: taken.AwayGoals,
                WinnerTeamId: taken.WinnerTeamId,
                IsSuddenDeath: taken.IsSuddenDeath)
            : null;
}
