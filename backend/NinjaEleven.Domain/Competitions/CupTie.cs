namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// One knockout tie: two clubs, two legs, and the answer.
///
/// The tie is the unit of a cup. It is a row of its own rather than something worked out by
/// looking at two fixtures, because a cup has to remember how a tie was decided: a tie won on
/// penalties and a tie won on the aggregate are the same result on a table and different
/// things in a club's history, and the shootout that settled it is the most memorable part of
/// the night for the men who took it.
/// </summary>
public class CupTie
{
    public Guid Id { get; private set; }
    public Guid CompetitionSeasonId { get; private set; }

    /// <summary>Which round of the bracket this tie belongs to, counted from one.</summary>
    public int RoundNumber { get; private set; }

    /// <summary>Who was at home in the first leg.</summary>
    public Guid HomeTeamId { get; private set; }

    /// <summary>Who was at home in the second leg — the other one.</summary>
    public Guid AwayTeamId { get; private set; }

    public Guid? FirstLegFixtureId { get; private set; }
    public Guid? SecondLegFixtureId { get; private set; }

    /// <summary>Goals the home club scored across both legs.</summary>
    public int? AggregateHomeGoals { get; private set; }

    /// <summary>Goals the away club scored across both legs.</summary>
    public int? AggregateAwayGoals { get; private set; }

    /// <summary>
    /// Penalties the home club scored in the shootout, and null when the tie was decided
    /// without one. A tie settled on penalties has this filled in and is still a win: the
    /// aggregate is recorded either way, because 3-3 is what happened.
    /// </summary>
    public int? HomePenaltyGoals { get; private set; }

    public int? AwayPenaltyGoals { get; private set; }

    /// <summary>Who goes through, and null while the tie has not been decided.</summary>
    public Guid? WinnerTeamId { get; private set; }

    /// <summary>
    /// Who goes out, and null while the tie has not been decided.
    ///
    /// It is kept rather than worked out by asking who the winner was not, because the losing
    /// side of a final is a fact in its own right: it is the runner-up, it goes on the shelf,
    /// and a final decided on penalties has a runner-up just as much as one decided 2-0.
    /// </summary>
    public Guid? LoserTeamId { get; private set; }

    private CupTie() { }

    public static CupTie Create(
        Guid competitionSeasonId,
        int roundNumber,
        Guid homeTeamId,
        Guid awayTeamId)
    {
        if (roundNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundNumber), roundNumber, "A cup round is counted from one.");
        }

        if (homeTeamId == awayTeamId)
        {
            throw new ArgumentException("A club cannot tie with itself.", nameof(homeTeamId));
        }

        return new CupTie
        {
            Id = Guid.NewGuid(),
            CompetitionSeasonId = competitionSeasonId,
            RoundNumber = roundNumber,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId
        };
    }

    public void SetLegs(Guid firstLegFixtureId, Guid secondLegFixtureId)
    {
        if (firstLegFixtureId == secondLegFixtureId)
        {
            throw new ArgumentException("A tie has two legs, not one fixture twice.", nameof(secondLegFixtureId));
        }

        FirstLegFixtureId = firstLegFixtureId;
        SecondLegFixtureId = secondLegFixtureId;
    }

    /// <summary>
    /// Records the aggregate and sends the tie to penalties if it is level, or straight to a
    /// winner if it is not. The caller passes the shootout in only when it was needed, so a
    /// tie that is decided on the aggregate is never given a shootout it did not have.
    ///
    /// The shootout is the one the match itself played, keyed by club rather than by side so
    /// that the legs swapping ends cannot swap the penalties along with them.
    /// </summary>
    public void Resolve(int aggregateHomeGoals, int aggregateAwayGoals, ShootoutOutcome? shootout = null)
    {
        if (AggregateHomeGoals is not null)
        {
            throw new InvalidOperationException("This tie has already been decided.");
        }

        if (aggregateHomeGoals < 0 || aggregateAwayGoals < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aggregateHomeGoals), "An aggregate is a number of goals and cannot be negative.");
        }

        AggregateHomeGoals = aggregateHomeGoals;
        AggregateAwayGoals = aggregateAwayGoals;

        if (aggregateHomeGoals > aggregateAwayGoals)
        {
            WinnerTeamId = HomeTeamId;
            LoserTeamId = AwayTeamId;
            return;
        }

        if (aggregateAwayGoals > aggregateHomeGoals)
        {
            WinnerTeamId = AwayTeamId;
            LoserTeamId = HomeTeamId;
            return;
        }

        // Level on the aggregate. There is no extra time: a cup tie that is level goes
        // straight to penalties, and a tie that is not decided by a shootout is not decided.
        if (shootout is null)
        {
            throw new InvalidOperationException(
                "The aggregate is level, so this tie has to be decided by a shootout.");
        }

        if (shootout.WinnerTeamId == Guid.Empty
            || (shootout.WinnerTeamId != HomeTeamId && shootout.WinnerTeamId != AwayTeamId))
        {
            throw new InvalidOperationException("A shootout always has one of the two clubs as its winner.");
        }

        HomePenaltyGoals = shootout.HomeGoals;
        AwayPenaltyGoals = shootout.AwayGoals;
        WinnerTeamId = shootout.WinnerTeamId == HomeTeamId ? HomeTeamId : AwayTeamId;
        LoserTeamId = WinnerTeamId == HomeTeamId ? AwayTeamId : HomeTeamId;
    }

    public bool IsResolved => WinnerTeamId is not null;

    /// <summary>Whether this tie was decided by penalties, which is how a cup is remembered.</summary>
    public bool WentToPenalties => HomePenaltyGoals is not null;
}
