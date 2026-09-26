using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Common;
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

    public async Task<IReadOnlyList<FixtureDetails>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var fixtures = await _fixtureRepository.ListAsync(cancellationToken);
        var details = new List<FixtureDetails>(fixtures.Count);

        foreach (var fixture in fixtures)
        {
            details.Add(await EnrichAsync(fixture, cancellationToken));
        }

        return details;
    }

    public async Task<FixtureDetails> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fixture = await _fixtureRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Fixture", id);

        return await EnrichAsync(fixture, cancellationToken);
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
        var results = new List<FixtureDetails>(fixtures.Count);

        foreach (var fixture in fixtures)
        {
            results.Add(await EnrichAsync(fixture, cancellationToken));
        }

        return results;
    }

    private async Task<FixtureDetails> EnrichAsync(Fixture fixture, CancellationToken cancellationToken)
    {
        var homeTeam = await _teamRepository.GetAsync(fixture.HomeTeamId, cancellationToken);
        var awayTeam = await _teamRepository.GetAsync(fixture.AwayTeamId, cancellationToken);
        var match = await _matchRepository.GetByFixtureAsync(fixture.Id, cancellationToken);

        return new FixtureDetails
        {
            Fixture = fixture,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            Match = match
        };
    }
}
