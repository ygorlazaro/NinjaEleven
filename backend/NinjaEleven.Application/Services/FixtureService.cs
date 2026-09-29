using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

public class FixtureService
{
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IRoundRepository _roundRepository;

    public FixtureService(
        IFixtureRepository fixtureRepository,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        IRoundRepository roundRepository)
    {
        _fixtureRepository = fixtureRepository;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _roundRepository = roundRepository;
    }

    /// <summary>
    /// Every fixture of the world, with the two clubs and the match each one is about.
    ///
    /// The clubs and the matches are read in three queries rather than one per fixture: a
    /// season is five hundred fixtures and the two clubs of all of them are forty-eight names,
    /// so a fixture list that asked per fixture was asking the same forty-eight questions five
    /// hundred times — and it is asked on every load of the calendar and of the championship.
    /// </summary>
    public async Task<IReadOnlyList<FixtureDetails>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var fixtures = await _fixtureRepository.ListAsync(cancellationToken);

        return await EnrichManyAsync(fixtures, cancellationToken);
    }

    public async Task<FixtureDetails> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", id);

        return (await EnrichManyAsync([fixture], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<FixtureDetails>> GetByRoundAsync(
        Guid roundId,
        CancellationToken cancellationToken = default)
    {
        if (await _roundRepository.GetAsync(roundId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Round", roundId);
        }

        var fixtures = await _fixtureRepository.ListByRoundAsync(roundId, cancellationToken);

        return await EnrichManyAsync(fixtures, cancellationToken);
    }

    /// <summary>
    /// A list of fixtures carrying the two clubs and the match behind each one, read in three
    /// queries. The match of a fixture is the newest row of it that was not abandoned, so a
    /// fixture that was interrupted and replayed still shows the match anybody is watching.
    /// </summary>
    private async Task<IReadOnlyList<FixtureDetails>> EnrichManyAsync(
        IReadOnlyList<Fixture> fixtures,
        CancellationToken cancellationToken)
    {
        if (fixtures.Count == 0)
        {
            return Array.Empty<FixtureDetails>();
        }

        var clubs = await _teamRepository.ListByIdsAsync(
            fixtures.SelectMany(fixture => new[] { fixture.HomeTeamId, fixture.AwayTeamId }).Distinct(),
            cancellationToken);
        var clubById = clubs.ToDictionary(club => club.Id);

        // Ordered by creation, so the last row per fixture is the newest one that stands.
        var matches = await _matchRepository.ListByFixtureIdsAsync(
            fixtures.Select(fixture => fixture.Id),
            cancellationToken);
        var matchByFixture = matches
            .Where(match => match.Status != MatchStatus.Abandoned)
            .GroupBy(match => match.FixtureId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(match => match.CreatedAt).First());

        return fixtures
            .Select(fixture => new FixtureDetails
            {
                Fixture = fixture,
                HomeTeam = clubById.GetValueOrDefault(fixture.HomeTeamId),
                AwayTeam = clubById.GetValueOrDefault(fixture.AwayTeamId),
                Match = matchByFixture.GetValueOrDefault(fixture.Id)
            })
            .ToList();
    }
}
