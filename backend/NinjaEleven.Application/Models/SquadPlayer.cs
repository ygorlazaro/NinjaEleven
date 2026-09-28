using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Models;

/// <summary>
/// A player as seen from a squad: static identity plus the state he has in the season.
/// </summary>
public class SquadPlayer
{
    public required Player Player { get; init; }
    public required PlayerSeasonState SeasonState { get; init; }

    /// <summary>
    /// The contract the club holds him on, and the season it is being read against. The two
    /// travel together because how much of the contract is left is a question about both: a
    /// membership says when the promise started and a season says where in the calendar we
    /// are, and neither answers it alone.
    ///
    /// The membership is missing for a player who is on a club's season state and on nobody's
    /// contract, and nothing is invented for him. A man on no contract is on no contract: he
    /// has no years left to buy and no fine to pay, and a squad table that gave him a
    /// three-season deal to have a number in the column would be inventing the one fact the
    /// column is about.
    /// </summary>
    public TeamMembership? Membership { get; init; }

    public required Season Season { get; init; }

    public bool IsAvailable => SeasonState.IsAvailable;

    /// <summary>
    /// Whether the player has declared he will retire at the end of the season. The flag travels
    /// with the squad so the table can show the walking-away icon without a second lookup.
    /// </summary>
    public bool Retiring => SeasonState.Retiring;

    /// <summary>
    /// How many seasons the club has him signed for, which is what his wage is a share of.
    /// It travels with the squad rather than being looked up per screen, so a wage shown on a
    /// squad table and the wage charged to the club's book are read from the same contract.
    /// </summary>
    public int ContractSeasons => Membership?.ContractSeasons ?? 0;

    /// <summary>
    /// How many seasons of his contract are left, one being the last one. Zero for a player
    /// on no contract at all, which is the truth rather than a missing number: there is
    /// nothing left of a contract that was never there.
    /// </summary>
    public int SeasonsLeft => Membership?.SeasonsLeft(Season.Number) ?? 0;

    /// <summary>
    /// Whether a rival can sign him without buying years he is still under contract for. A
    /// player on no contract always can, and so pays no fine — there is nothing to break.
    /// </summary>
    public bool IsInLastSeason => Membership is null || Membership.IsInHisLastSeason(Season.Number);

    /// <summary>What he is worth on the market, in limos.</summary>
    public decimal MarketValue => PlayerValuation.MarketValue(Player, SeasonState);

    /// <summary>
    /// What another club would have to pay for him, in limos: his value, and a fifth more of
    /// it while he is not in the last season of his contract. The two are kept apart because
    /// they answer two different questions — what he is worth to the club that has him, and
    /// what it costs to take him off them.
    /// </summary>
    public decimal AskingPrice => PlayerValuation.AskingPrice(MarketValue, IsInLastSeason);

    /// <summary>What the club owes for his contract this season, in limos.</summary>
    public decimal Salary => PlayerValuation.SeasonWage(Player, SeasonState);
}

/// <summary>
/// A fixture enriched with both clubs and the result of the match played from it.
/// </summary>
public class FixtureDetails
{
    public required Fixture Fixture { get; init; }
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
    public Match? Match { get; init; }
}

/// <summary>
/// Everything a client needs to re-render a match: the session state plus the ordered
/// event log. This is the snapshot served by the REST endpoint that reconnects a client
/// before it resumes following SignalR.
/// </summary>
public class MatchSnapshot
{
    public required Match Match { get; init; }
    public IReadOnlyList<MatchEvent> Events { get; init; } = Array.Empty<MatchEvent>();
    public Team? HomeTeam { get; init; }
    public Team? AwayTeam { get; init; }
}

/// <summary>
/// Result of creating a competition edition: the edition itself plus the generated
/// schedule.
/// </summary>
public class LeagueSetup
{
    public required Guid CompetitionSeasonId { get; init; }
    public required Guid SeasonId { get; init; }
    public required Guid CompetitionId { get; init; }
    public IReadOnlyList<FixtureDetails> Fixtures { get; init; } = Array.Empty<FixtureDetails>();
}
