using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// How strong a club is, for the one thing a comparison falls back on.
/// </summary>
/// <para>
/// It is its own reader because a squad's strength is asked about by more than the standings:
/// a table's last tiebreak and a cup's ranking want the same number for the same club in the
/// same season, and a measurement with two copies is a measurement that will be changed in one
/// of them. The seeding of the world and the strength a sponsor reads are the domain's own
/// <c>ClubStrength</c>; this is the reader that answers a set of clubs at once, because the
/// strength of a table is the same fact for every club in it.
/// </para>
public interface ISquadStrengthReader
{
    /// <summary>
    /// The strength of each of these clubs in this season, and zero for one that has no squad.
    /// </summary>
    /// <param name="teamIds">The clubs to measure, as one set.</param>
    /// <param name="seasonId">The season whose squads are being measured.</param>
    Task<IReadOnlyDictionary<Guid, double>> ForTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        Guid seasonId,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class SquadStrengthReader : ISquadStrengthReader
{
    private readonly ITeamRepository _teamRepository;

    public SquadStrengthReader(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    /// <remarks>
    /// The squads and the players behind them are read in two queries rather than one per club
    /// and one per man. A reader that asked club by club and player by player was three hundred
    /// round trips to say twelve numbers, which is what made a division's table time out rather
    /// than arrive — and the same reader now answers for a whole cup.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, double>> ForTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        var strength = new Dictionary<Guid, double>();

        if (teamIds.Count == 0)
        {
            return strength;
        }

        var squads = await _teamRepository.GetSquadsAsync(teamIds, seasonId, cancellationToken);
        var playerIds = squads.Values
            .SelectMany(memberships => memberships)
            .Select(membership => membership.PlayerId)
            .Distinct()
            .ToList();
        var players = playerIds.Count == 0
            ? new Dictionary<Guid, Player>()
            : await _teamRepository.GetPlayersAsync(playerIds, cancellationToken);

        foreach (var teamId in teamIds)
        {
            var squad = squads.TryGetValue(teamId, out var memberships)
                ? memberships
                    .Select(membership => players.GetValueOrDefault(membership.PlayerId))
                    .Where(player => player is not null)
                    .Select(player => player!)
                    .ToList()
                : new List<Player>();

            strength[teamId] = ClubStrength.Calculate(squad);
        }

        return strength;
    }
}