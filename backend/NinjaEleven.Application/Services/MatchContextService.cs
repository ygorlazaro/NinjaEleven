using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Services;

/// <summary>
/// Where a match is, and what kind of match it is.
///
/// It answers from the chain a fixture actually belongs to rather than from the match row: a
/// match knows its fixture, a fixture knows its window, a window knows its edition and its
/// day, and only the edition knows whether this is a division, a cup or a Supercup. Reading
/// it anywhere else means a screen that guesses — and a guess about a cup is a guess about
/// whether there is a return leg, which is the difference between a tie and a match.
///
/// The result of the other leg comes from the tie rather than from a second query a client
/// would have to know to make: a manager reading a 1 x 0 under "partida de volta" needs to
/// know the aggregate, and the client cannot work it out from anything it is given.
/// </summary>
public class MatchContextService
{
    private readonly IMatchRepository _matchRepository;
    private readonly IFixtureRepository _fixtureRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IMatchDayRepository _matchDayRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly ICupTieRepository _cupTieRepository;
    private readonly ITeamRepository _teamRepository;

    public MatchContextService(
        IMatchRepository matchRepository,
        IFixtureRepository fixtureRepository,
        IRoundRepository roundRepository,
        ICompetitionRepository competitionRepository,
        IMatchDayRepository matchDayRepository,
        ISeasonRepository seasonRepository,
        ICupTieRepository cupTieRepository,
        ITeamRepository teamRepository)
    {
        _matchRepository = matchRepository;
        _fixtureRepository = fixtureRepository;
        _roundRepository = roundRepository;
        _competitionRepository = competitionRepository;
        _matchDayRepository = matchDayRepository;
        _seasonRepository = seasonRepository;
        _cupTieRepository = cupTieRepository;
        _teamRepository = teamRepository;
    }

    /// <summary>
    /// The context of one match, or null when the match is not there.
    /// </summary>
    public async Task<MatchContextView?> GetAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetAsync(matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var fixture = await _fixtureRepository.GetAsync(match.FixtureId, cancellationToken);
        if (fixture is null)
        {
            return null;
        }

        var window = await _roundRepository.GetAsync(fixture.RoundId, cancellationToken);
        if (window is null)
        {
            return null;
        }

        var edition = await _competitionRepository.GetSeasonViewByIdAsync(
            window.CompetitionSeasonId, cancellationToken);

        if (edition is null)
        {
            return null;
        }

        var season = await _seasonRepository.GetAsync(edition.SeasonId, cancellationToken);

        var day = window.MatchDayId is null
            ? null
            : await _matchDayRepository.GetAsync(window.MatchDayId.Value, cancellationToken);

        var home = await _teamRepository.GetAsync(match.HomeTeamId, cancellationToken);

        return new MatchContextView
        {
            SeasonName = season?.Name ?? string.Empty,
            MatchDayNumber = day?.Number ?? 0,
            CompetitionName = edition.CompetitionName,
            CompetitionType = edition.Type,
            EditionName = edition.Name,
            PhaseName = PhaseOf(edition, window),
            LegLabel = LegLabelOf(edition, await _cupTieRepository.GetByLegAsync(fixture.Id, cancellationToken), fixture.Id),
            StadiumName = home?.Stadium?.Name ?? string.Empty,
            StadiumCapacity = home?.Stadium?.Capacity ?? 0,
            FirstLeg = await ReadTheOtherLegAsync(match.FixtureId, edition.Type, cancellationToken)
        };
    }

    /// <summary>
    /// The phase of the competition, said the way a manager says it.
    ///
    /// A championship is a sequence of rounds and the round is its phase. A cup is a knockout
    /// and its phase is the round of the bracket, which the tie knows and the window does not.
    /// A Supercup is one match and has no phase of its own, so it says so.
    /// </summary>
    private static string PhaseOf(CompetitionSeasonView edition, Round window) => edition.Type switch
    {
        CompetitionType.League => $"Rodada {window.Number}",
        CompetitionType.Cup => CompetitionRules.TieRoundName(
            CompetitionRules.CupTieRoundOf(window.Number)),
        _ => "Final"
    };

    /// <summary>
    /// Which leg of the tie this is, or nothing at all when it is not a cup match.
    /// </summary>
    private static string? LegLabelOf(CompetitionSeasonView edition, CupTie? tie, Guid fixtureId)
    {
        if (edition.Type is not CompetitionType.Cup || tie is null)
        {
            return null;
        }

        return tie.FirstLegFixtureId == fixtureId ? "partida de ida" : "partida de volta";
    }

    /// <summary>
    /// The leg before this one, when this match is the second one of a tie.
    ///
    /// It is the *finished* leg, so the score comes from the match that played it: the fixture
    /// says a match happened and the match says what it was, and a cup tie that is being
    /// decided has no aggregate to read from anywhere else.
    /// </summary>
    private async Task<CupLegResult?> ReadTheOtherLegAsync(
        Guid fixtureId,
        CompetitionType type,
        CancellationToken cancellationToken)
    {
        if (type is not CompetitionType.Cup)
        {
            return null;
        }

        var tie = await _cupTieRepository.GetByLegAsync(fixtureId, cancellationToken);

        if (tie?.FirstLegFixtureId is null || tie.SecondLegFixtureId != fixtureId)
        {
            return null;
        }

        var firstLeg = await _matchRepository.GetByFixtureAsync(
            tie.FirstLegFixtureId.Value, cancellationToken);

        if (firstLeg is null)
        {
            return null;
        }

        var home = await _teamRepository.GetAsync(firstLeg.HomeTeamId, cancellationToken);
        var away = await _teamRepository.GetAsync(firstLeg.AwayTeamId, cancellationToken);

        return new CupLegResult
        {
            HomeTeamId = firstLeg.HomeTeamId,
            HomeTeamName = home?.Name ?? string.Empty,
            HomeGoals = firstLeg.HomeScore,
            AwayTeamId = firstLeg.AwayTeamId,
            AwayTeamName = away?.Name ?? string.Empty,
            AwayGoals = firstLeg.AwayScore
        };
    }
}
