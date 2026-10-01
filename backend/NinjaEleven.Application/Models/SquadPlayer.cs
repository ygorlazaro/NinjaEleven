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

    /// <summary>
    /// The number this man wears for this club, or null while nobody has dealt him one.
    ///
    /// It is read off the contract rather than stored beside it, for the same reason the
    /// seasons left are: the membership is the thing that knows, and a second copy on the
    /// squad row would be a number two screens could disagree about. A null here is a real
    /// state and not a missing column — it is a world seeded before shirts existed, which the
    /// seeder fills in on the next boot.
    /// </summary>
    public int? ShirtNumber => Membership?.ShirtNumber;

    /// <summary>What he is worth on the market, in limos.</summary>
    public decimal MarketValue => PlayerValuation.MarketValue(Player, SeasonState);

    /// <summary>
    /// What another club would have to pay for him, in limos: his value, and a fifth more of
    /// it while he is not in the last season of his contract. The two are kept apart because
    /// they answer two different questions — what he is worth to the club that has him, and
    /// what it costs to take him off them.
    /// </summary>
    public decimal AskingPrice => PlayerValuation.AskingPrice(MarketValue, IsInLastSeason);

    /// <summary>
    /// What the club owes him for this season, in limos, and what it will go on owing for as
    /// long as this contract runs.
    ///
    /// It is the number on the contract rather than a figure worked out from his attributes
    /// today, so the salary a manager reads here is the salary the club's book is charged. A
    /// wage that moved with his form would mean a striker's price on this table and his cost
    /// in the wage bill were two different numbers, and a manager budgeting a season would be
    /// budgeting one of them. A man on no contract is owed nothing, which is the truth about a
    /// man nobody is paying.
    /// </summary>
    public decimal Salary => Membership?.Wage ?? 0m;

    /// <summary>
    /// What the club would pay him if it signed him again today, at the current formula and
    /// the age he is now.
    ///
    /// It is on the squad row because a renewal is a decision the manager takes from this
    /// screen, and a decision taken without seeing the number is a decision taken blind. It is
    /// deliberately not the salary above: the one is what he costs, the other is what he would
    /// cost, and a club that renews a man on the second number is choosing to pay more than it
    /// is paying now.
    /// </summary>
    public decimal WageOnRenewal => PlayerValuation.SeasonWage(Player, SeasonState);
}

/// <summary>
/// What a renewal settled, so the screen that asked for it can be told rather than
/// re-deriving the new wage from the squad it is about to refetch.
/// </summary>
/// <param name="PlayerId">Who was signed again.</param>
/// <param name="TeamId">Who signed him.</param>
/// <param name="SeasonId">The season the renewal was made in.</param>
/// <param name="ContractId">The contract that was restated.</param>
/// <param name="Seasons">How many seasons were signed, counted from now.</param>
/// <param name="SeasonsLeft">How many of them are left, which is the same number today.</param>
/// <param name="Wage">What he costs a season under the new deal.</param>
public record ContractRenewal(
    Guid PlayerId,
    Guid TeamId,
    Guid SeasonId,
    Guid ContractId,
    int Seasons,
    int SeasonsLeft,
    decimal Wage);

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
