namespace NinjaEleven.Domain.Teams;

/// <summary>
/// Everything the world knows about one club when it decides whether to build.
/// </summary>
/// <remarks>
/// Read as a set rather than gathered from the entity, because the decision is made about the
/// whole country at once and a caller that assembled this from a club would have to fetch a club
/// sixty-four times to make one pass.
/// </remarks>
public readonly record struct NpcStadiumFacts(
    Guid ClubId,
    int Capacity,
    /// <summary>What the crowd wanted this season, and what the ground turned away.</summary>
    int Demand,
    /// <summary>
    /// False when the club's matches were played before the world kept what the crowd wanted.
    /// It is not a demand of zero — it is a question nobody has answered.
    /// </summary>
    bool DemandIsMeasured,
    /// <summary>Whether the club's following went up over the season.</summary>
    bool CrowdIsGrowing,
    decimal Balance,
    /// <summary>What the club's whole squad costs for the season, at the going rate.</summary>
    decimal SeasonWages);

/// <summary>
/// Why a club did or did not start a project.
/// </summary>
public enum NpcStadiumVerdict
{
    /// <summary>Start it.</summary>
    Build,

    /// <summary>The ground holds everybody who comes.</summary>
    TheGroundFits,

    /// <summary>
    /// The club is nearly full and its following is still growing, so next season it will not
    /// be. This is the reason a club builds before it has to.
    /// </summary>
    TheCrowdIsOutgrowingTheGround,

    /// <summary>Nobody has measured what this club's crowd wants.</summary>
    NotYetMeasured,

    /// <summary>The project exists and this club cannot carry it.</summary>
    CannotAfford,

    /// <summary>The ground is already as big as this game builds.</summary>
    NothingLeftToBuild
}

/// <summary>
/// A club's answer, and the project it is about.
/// </summary>
public readonly record struct NpcStadiumDecision(
    NpcStadiumVerdict Verdict,
    StadiumProject? Project,
    decimal Cost)
{
    /// <summary>Whether this decision asks for a building site.</summary>
    public bool Builds => Verdict == NpcStadiumVerdict.Build;
}

/// <summary>
/// When the clubs nobody is running build, and what they take into account.
///
/// <para>
/// A world where only the one club a person is in ever grows is a world of sixty-four clubs
/// whose grounds are frozen on the day they were seeded, and a crowd model that is supposed to
/// press the first division to spend four million on a stand would be pressing nobody at all.
/// So the same question a manager answers with a button is answered for the rest of the country
/// on a schedule, out of the same two things a manager looks at: <b>how badly the ground is
/// wanted</b> and <b>whether the club can carry it</b>.
/// </para>
///
/// <para>
/// Every five rounds rather than every round, because a stand is built once and a crowd is
/// measured over a season. A club assessed on one afternoon's gate would build on a wet
/// Saturday, and the schedule is what stops the whole pyramid from starting a building site on
/// the same matchday and the country's gate money from arriving in one lump.
/// </para>
///
/// <para>
/// <b>The two numbers are independent, and either one can stop it.</b> Need without money is a
/// club that wants a stand it cannot pay for; money without need is a club with a full bank
/// and nowhere to put it. The world has plenty of both and they are not the same clubs.
/// </para>
/// </summary>
public static class NpcStadiumPolicy
{
    /// <summary>
    /// How often the country looks at its grounds, in championship rounds.
    /// </summary>
    public const int EveryRounds = 5;

    /// <summary>
    /// How full a ground has to be before a club whose following is still growing is allowed
    /// to build ahead of the crowd.
    /// </summary>
    /// <remarks>
    /// The one place this policy looks forward instead of at what has already happened. A club
    /// that waits until it is turning people away to build is a club that is a year late: the
    /// stand it eventually raises is paid for with a full season of gates it could not collect.
    /// Fifteen per cent of headroom is roughly a season's growth for a club in the middle of
    /// its division, so a ground that full and a club that size is a club about to be short of
    /// seats rather than one that has been.
    /// </remarks>
    public const double AnticipationFill = 0.85;

    /// <summary>
    /// The decision, given what is known about the club.
    /// </summary>
    /// <remarks>
    /// The order of the questions is the order of how much they disqualify. Not knowing is
    /// first, because a club the world has never measured is not a club with no pressure; the
    /// money is last, because there is no point pricing a stand a club has no reason to want.
    /// </remarks>
    public static NpcStadiumDecision Decide(NpcStadiumFacts facts)
    {
        // A ground at the ceiling comes first of all, because it is the one answer that no
        // other question can change: there is nothing left to build whatever the crowd wants and
        // whatever the club has, so a club there is not a club with a full ground.
        if (StadiumRules.SmallestStepFor(facts.Capacity) is null)
        {
            return new NpcStadiumDecision(NpcStadiumVerdict.NothingLeftToBuild, null, 0m);
        }

        // Not knowing comes before everything, and it comes first on purpose. A club the world
        // has never measured is not a club with no pressure — it is a club whose pressure was
        // never taken, and a policy that read that as zero would spend a season deciding that
        // the ground is big enough on the strength of a question nobody asked.
        if (!facts.DemandIsMeasured)
        {
            return new NpcStadiumDecision(NpcStadiumVerdict.NotYetMeasured, null, 0m);
        }

        var wanted = ProjectWorthHaving(facts);

        if (wanted is null)
        {
            // Nothing to build means nothing to price. A "no" that carries a four-million-limo
            // stand on it is a decision waiting to be misread as a bill.
            return new NpcStadiumDecision(NpcStadiumVerdict.TheGroundFits, null, 0m);
        }

        if (!CanAfford(facts, wanted.Value.Cost))
        {
            return new NpcStadiumDecision(
                NpcStadiumVerdict.CannotAfford, wanted, wanted.Value.Cost);
        }

        return new NpcStadiumDecision(NpcStadiumVerdict.Build, wanted, wanted.Value.Cost);
    }

    /// <summary>
    /// The project a club should start, or null when its ground is the right size for now.
    /// </summary>
    /// <remarks>
    /// The size is asked for rather than the biggest step available. A club whose crowd wants
    /// eleven thousand is given the stand that fills it and not the largest one in the
    /// catalogue, and a club that wanted twenty and cannot be given twenty is not handed
    /// thirty: it is given the smallest step, which is a ground that grows over a season rather
    /// than a decision that empties a bank.
    /// </remarks>
    private static StadiumProject? ProjectWorthHaving(NpcStadiumFacts facts)
    {
        // Turning people away is the reason, and it needs no forecast.
        if (facts.Demand > facts.Capacity)
        {
            return StadiumRules.ProjectToReach(facts.Capacity, facts.Demand)
                ?? StadiumRules.SmallestStepFor(facts.Capacity);
        }

        // Nobody is being turned away, so the only reason left is that next season they will
        // be — and only a club whose following is rising can say that with any confidence.
        if (!facts.CrowdIsGrowing)
        {
            return null;
        }

        var occupancy = facts.Capacity <= 0 ? 0 : (double)facts.Demand / facts.Capacity;

        return occupancy >= AnticipationFill
            ? StadiumRules.SmallestStepFor(facts.Capacity)
            : null;
    }

    /// <summary>
    /// Whether the club can carry the stand and still pay its squad.
    /// </summary>
    /// <remarks>
    /// The reserve is the season's wages, and it is the wage bill rather than a multiple of the
    /// price on purpose. A stand is a cost a club can choose to defer; a squad is a cost it
    /// cannot, and a club that signs off its wage bill to buy seats has not improved its ground
    /// — it has moved the failure from one part of the season to another, where it happens
    /// earlier and cannot be refused.
    ///
    /// <para>
    /// It is the whole wage bill and not the rest of it, which makes this a deliberately
    /// generous test: a club that can still pay a season of wages after building is not about
    /// to go bankrupt, whatever the season ahead holds. A stricter rule would need a forecast of
    /// income the world does not keep, and a policy built on a forecast nobody measured is a
    /// policy with a number in it that came from nowhere.
    /// </para>
    /// </remarks>
    public static bool CanAfford(NpcStadiumFacts facts, decimal cost) =>
        facts.Balance - cost >= facts.SeasonWages;

    /// <summary>
    /// Whether the country looks at its grounds on this round.
    /// </summary>
    /// <remarks>
    /// Zero is not one of these days. A world that has played no championship round has not
    /// measured a single crowd, and looking at sixty-four grounds on the strength of nothing is
    /// the pass this whole policy exists to avoid.
    /// </remarks>
    public static bool IsBuildingDay(int playedThroughRound) =>
        playedThroughRound > 0 && playedThroughRound % EveryRounds == 0;
}