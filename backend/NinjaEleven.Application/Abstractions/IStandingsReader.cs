using NinjaEleven.Application.Models;

namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Reads one classification table.
/// </summary>
/// <para>
/// It is its own interface because the table is asked about by callers that need nothing else
/// of the standings service: a sponsor pass needs every division's table and not the club page,
/// the cup exposure and the head-to-head, and depending on the whole service for one method is
/// how a test ends up assembling a season's fixtures to answer a question about one line.
/// </para>
/// </summary>
public interface IStandingsReader
{
    Task<CompetitionStandings> GetAsync(Guid competitionSeasonId, CancellationToken cancellationToken = default);
}