using NinjaEleven.Domain.Common;

namespace NinjaEleven.Domain.Matches;

/// <summary>
/// A penalty shootout, taken one kick at a time.
///
/// It used to be a single random draw behind the result — a number out of a loop, written
/// to the tie without anybody watching — and that is exactly the thing this type is for. A
/// shootout is the part of a cup tie the two managers take part in: each side names the
/// order of its five takers, the kicks are taken in front of the crowd, and the screen
/// shows them one at a time. So the rules live here, as rules, and the engine asks this
/// object what the next kick is instead of drawing an answer in one go.
///
/// The Laws of the Game, and what each of them is doing in here:
///
///   **The toss decides who kicks first, and the team that wins it chooses.** The winner is
///   drawn once, from the tie's own seed, so a replayed tie takes the same kicks in the same
///   order. The side that wins it goes first, and no side is asked to choose twice.
///
///   **The kicks alternate.** One from each side in turn, and the side that has taken fewer
///   is the one whose turn it is — which is the same answer in the first five and in sudden
///   death, so it is one rule rather than two.
///
///   **Five each, then sudden death.** The first five are a race: a side that is further
///   ahead than the other has kicks left is the winner, and the shootout stops there. A side
///   two up after three kicks has won, and taking two more is theatre.
///
///   **Sudden death ends on the first difference.** One kick each, and the side that is
///   ahead when the pair is through has won. A pair that ends level is not a draw, it is two
///   more kicks.
///
///   **A man kicks once.** Every kick is taken by a different player, and a player who has
///   taken one may only take another after everybody else has — which is what the order is
///   for, and why the order is the manager's to name rather than the engine's.
/// </summary>
public sealed class Shootout
{
    /// <summary>Kicks each side takes before sudden death starts.</summary>
    public const int KicksPerSide = 5;

    /// <summary>
    /// A floor under sudden death. Two sides that both score every kick are a measure-zero
    /// event, but a loop that can only end by chance still needs an end.
    /// </summary>
    private const int MaxSuddenDeathPairs = 50;

    private readonly List<ShootoutKick> _kicks = new();

    private Shootout(Guid homeTeamId, Guid awayTeamId, bool homeTakesFirst)
    {
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        HomeTakesFirst = homeTakesFirst;
    }

    /// <summary>The club that was at home in the second leg.</summary>
    public Guid HomeTeamId { get; }

    /// <summary>The club that was away in the second leg.</summary>
    public Guid AwayTeamId { get; }

    /// <summary>
    /// Who the toss sent first. Drawn once, before anybody kicks, and it is the only thing
    /// the toss decides: the side that goes first is not asked to choose anything after it.
    /// </summary>
    public bool HomeTakesFirst { get; }

    /// <summary>
    /// The five men each side will take, in the order it named them. The manager's club
    /// names his; the other side is drawn by the engine, in the order a manager would have
    /// picked them, which is the same order the taker screen shows.
    /// </summary>
    public IReadOnlyList<Guid> HomeTakers { get; private set; } = Array.Empty<Guid>();

    public IReadOnlyList<Guid> AwayTakers { get; private set; } = Array.Empty<Guid>();

    /// <summary>Every kick taken so far, in the order they were taken.</summary>
    public IReadOnlyList<ShootoutKick> Kicks => _kicks;

    public bool IsComplete { get; private set; }

    /// <summary>Empty until the shootout is decided. A shootout is never a draw.</summary>
    public Guid WinnerTeamId { get; private set; } = Guid.Empty;

    public int HomeGoals => _kicks.Count(kick => kick.TeamId == HomeTeamId && kick.Scored);

    public int AwayGoals => _kicks.Count(kick => kick.TeamId == AwayTeamId && kick.Scored);

    public int HomeKicksTaken => _kicks.Count(kick => kick.TeamId == HomeTeamId);

    public int AwayKicksTaken => _kicks.Count(kick => kick.TeamId == AwayTeamId);

    /// <summary>
    /// Sudden death is the first kick taken after both sides have had their five. It is a
    /// fact about where the shootout is rather than a flag a screen sets.
    /// </summary>
    public bool IsSuddenDeath => HomeKicksTaken > KicksPerSide || AwayKicksTaken > KicksPerSide;

    /// <summary>
    /// Starts a shootout with nobody's order named yet.
    ///
    /// The two sides' orders arrive separately and from two different places — one is a
    /// manager's decision and the other is the engine's — and the shootout waits for both
    /// rather than being started with a default. A shootout whose first kick is taken by
    /// whoever happened to be at the front of a list is a shootout nobody entered.
    /// </summary>
    public static Shootout Begin(Guid homeTeamId, Guid awayTeamId, bool homeTakesFirst)
    {
        if (homeTeamId == awayTeamId)
        {
            throw new ArgumentException("A shootout needs two clubs.", nameof(homeTeamId));
        }

        return new Shootout(homeTeamId, awayTeamId, homeTakesFirst);
    }

    /// <summary>
    /// Starts a shootout whose two orders are both already named, which is the case when
    /// nobody is watching and both sides are the engine's own choice.
    /// </summary>
    /// <param name="homeTakers">The home club's takers, in order.</param>
    /// <param name="awayTakers">The away club's takers, in order.</param>
    public static Shootout Begin(
        Guid homeTeamId,
        Guid awayTeamId,
        bool homeTakesFirst,
        IReadOnlyList<Guid> homeTakers,
        IReadOnlyList<Guid> awayTakers)
    {
        var shootout = Begin(homeTeamId, awayTeamId, homeTakesFirst);
        shootout.SetOrder(home: true, homeTakers);
        shootout.SetOrder(home: false, awayTakers);
        return shootout;
    }

    /// <summary>
    /// Names the order one side will take in, and only once.
    /// </summary>
    /// <param name="home">Which of the two sides is naming its order.</param>
    /// <param name="takers">The men, in the order they will walk to the spot.</param>
    public void SetOrder(bool home, IReadOnlyList<Guid> takers)
    {
        ArgumentNullException.ThrowIfNull(takers);

        if (takers.Count == 0)
        {
            throw new ArgumentException(
                "A side that cannot send anybody to the spot cannot take part in a shootout.",
                nameof(takers));
        }

        if (takers.Distinct().Count() != takers.Count)
        {
            throw new ArgumentException(
                "The same man cannot be named twice in one order: every kick is taken by a different player.",
                nameof(takers));
        }

        if (home ? HomeTakers.Count > 0 : AwayTakers.Count > 0)
        {
            throw new InvalidOperationException("This side's order of takers has already been named.");
        }

        if (home)
        {
            HomeTakers = takers.ToList();
        }
        else
        {
            AwayTakers = takers.ToList();
        }
    }

    /// <summary>
    /// Whether both sides have named who is taking, which is the condition for the first
    /// kick to be taken at all.
    /// </summary>
    public bool BothOrdersNamed => HomeTakers.Count > 0 && AwayTakers.Count > 0;

    /// <summary>
    /// Whose turn it is to kick, or null when the shootout is over.
    /// </summary>
    /// <remarks>
    /// The side with fewer kicks taken goes next, which is the alternation the Laws
    /// describe and the pair-by-pair structure of sudden death at the same time. The toss
    /// decides who is level first: the side it picked is the one that is a kick ahead of the
    /// other in the count, and the side behind is the one whose turn it is.
    /// </remarks>
    public bool? NextTeamIsHome
    {
        get
        {
            if (IsComplete)
            {
                return null;
            }

            if (HomeKicksTaken == AwayKicksTaken)
            {
                return HomeTakesFirst;
            }

            return HomeKicksTaken > AwayKicksTaken ? false : true;
        }
    }

    /// <summary>
    /// The man who takes the next kick for a side, or null when his side has nobody left to
    /// take one.
    /// </summary>
    /// <remarks>
    /// A side with fewer men than it has kicks to take comes back to the men it has already
    /// used, and it is the only time that can happen: a club that can only send four men
    /// cannot take five, and the Laws are explicit that the men who have kicked may kick
    /// again once everybody else has.
    /// </remarks>
    public Guid? NextTaker(bool home)
    {
        var order = home ? HomeTakers : AwayTakers;
        var teamId = home ? HomeTeamId : AwayTeamId;
        var taken = _kicks.Count(kick => kick.TeamId == teamId);

        if (order.Count == 0)
        {
            return null;
        }

        if (taken < order.Count)
        {
            return order[taken];
        }

        // Everybody the manager named has kicked. The men who have already taken are the
        // only ones left, so the first of them goes again.
        return order.Count > 0 ? order[0] : null;
    }

    /// <summary>
    /// Records one kick and works out whether the shootout is over.
    /// </summary>
    /// <param name="takerId">The man who took it.</param>
    /// <param name="scored">Whether it went in.</param>
    public ShootoutKick Take(Guid takerId, bool scored)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("A decided shootout takes no more kicks.");
        }

        if (NextTeamIsHome is not { } home)
        {
            throw new InvalidOperationException("A shootout between one club and itself is not a shootout.");
        }

        var teamId = home ? HomeTeamId : AwayTeamId;
        var kick = new ShootoutKick(teamId, takerId, scored);
        _kicks.Add(kick);

        Decide();
        return kick;
    }

    /// <summary>
    /// The end of the shootout, asked after every kick.
    ///
    /// In the first five it is a race with a finish line: a side ahead by more than the
    /// other's remaining kicks has won it, and the remaining kicks are not taken. From the
    /// sixth kick onwards the two sides have taken the same number and the first difference
    /// is the answer.
    /// </summary>
    private void Decide()
    {
        var home = HomeGoals;
        var away = AwayGoals;
        var pairs = Math.Min(HomeKicksTaken, AwayKicksTaken);

        if (pairs < KicksPerSide)
        {
            // Still in the first five. A lead settles the shootout only when the side that
            // is behind cannot catch it even by scoring every kick it has left — two up
            // after three kicks is a win, and one up after four is not. It is asked about
            // both sides, and not only the one that has just kicked: a side that was
            // already two up when its opponent missed its third is the winner of the tie at
            // that moment, and taking two more kicks would be theatre.
            var homeLeft = KicksPerSide - HomeKicksTaken;
            var awayLeft = KicksPerSide - AwayKicksTaken;

            if (home > away + awayLeft)
            {
                Finish(HomeTeamId);
            }
            else if (away > home + homeLeft)
            {
                Finish(AwayTeamId);
            }

            return;
        }

        // Sudden death: both sides have taken the same number of kicks, so a difference is
        // a decision. A level pair is two more kicks.
        if (HomeKicksTaken == AwayKicksTaken && home != away)
        {
            Finish(home > away ? HomeTeamId : AwayTeamId);
        }
    }

    /// <summary>
    /// Ends the shootout. There is one exit out of it and it is never a draw, which is what
    /// every question about whether a shootout can be level is really asking.
    /// </summary>
    private void Finish(Guid winner)
    {
        IsComplete = true;
        WinnerTeamId = winner;
    }

    /// <summary>
    /// Whether this shootout has gone on long enough to be a broken random source rather
    /// than football. It is asked by the loop rather than by the rules, which is why it is
    /// not part of the decision above.
    /// </summary>
    public bool HasRunOutOfPairs =>
        Math.Min(HomeKicksTaken, AwayKicksTaken) >= KicksPerSide + MaxSuddenDeathPairs;
}

/// <summary>
/// One kick of a shootout: who took it, for which side, and whether it went in.
/// </summary>
public readonly record struct ShootoutKick(Guid TeamId, Guid TakerId, bool Scored);
