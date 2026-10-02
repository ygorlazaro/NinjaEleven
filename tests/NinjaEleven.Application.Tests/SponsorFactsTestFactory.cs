using Moq;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Services;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// Builds the service that reads everything a sponsor's price depends on, over a world small
/// enough to be a test.
///
/// <para>
/// It is the real service over mocks rather than a mock of a service, because the reading is
/// part of what the sponsor tests are about: a club with no line on the table, a division of
/// one club, a club that has not played, a crowd of zero — these are the states the pricing
/// has to survive, and a mocked reader would hand back none of them.
/// </para>
/// </summary>
internal static class SponsorFactsTestFactory
{
    /// <summary>
    /// The facts reader and the competition repository it is built over.
    ///
    /// They come back together because a caller that prices a shirt asks the repository which
    /// division the club is in as well as asking the reader for the rest — and handing the
    /// service a second, different mock would answer one of the two questions and not the
    /// other, which is a price of zero for a club that is plainly in a division.
    /// </summary>
    internal sealed record World(SponsorClubFactsService Facts, Mock<ICompetitionRepository> Competitions);

    /// <summary>
    /// A club in a division of <paramref name="clubsInDivision"/>, at a given place, with a
    /// given form and a given crowd.
    /// </summary>
    public static World Create(
        Guid seasonId,
        Guid teamId,
        int tier = 1,
        int position = 1,
        int clubsInDivision = 16,
        double pointsPerGame = 1.5,
        double attendance = 2_500,
        bool stillInCup = false,
        int homeMatches = 5,
        params Guid[] alsoInDivision)
    {
        var competitions = new Mock<ICompetitionRepository>(MockBehavior.Loose);
        var matches = new Mock<IMatchRepository>(MockBehavior.Loose);
        var cupTies = new Mock<ICupTieRepository>(MockBehavior.Loose);
        var standings = new Mock<IStandingsReader>(MockBehavior.Loose);

        var division = new CompetitionSeasonView
        {
            Id = Guid.NewGuid(),
            CompetitionId = Guid.NewGuid(),
            SeasonId = seasonId,
            DivisionId = Guid.NewGuid(),
            Tier = tier,
            CompetitionName = CompetitionRules.DivisionName(tier),
            Type = CompetitionType.League
        };

        var row = new StandingRow
        {
            TeamId = teamId,
            Position = position,
            Played = 10,
            Wins = 3,
            Draws = 3,
            Losses = 4,
            GoalsFor = 10,
            GoalsAgainst = 8,
            YellowCards = 1,
            RedCards = 0,
            Stars = 50
        };

        competitions
            .Setup(repo => repo.ListSeasonViewsAsync(seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompetitionSeasonView> { division });

        // The other clubs of the division are participants and not rows on the table: a
        // sponsor's own book is asked whether it already has one of them, and a book of clubs
        // the division does not contain could never clash with anything.
        competitions
            .Setup(repo => repo.ListParticipantsAsync(division.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[] { teamId }.Concat(alsoInDivision)
                    .Select(club => CompetitionParticipant.Create(division.Id, club))
                    .ToList());

        competitions
            .Setup(repo => repo.GetDivisionSeasonForTeamAsync(teamId, seasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(division);

        standings
            .Setup(reader => reader.GetAsync(division.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompetitionStandings
            {
                CompetitionSeasonId = division.Id,
                SeasonId = seasonId,
                DivisionId = division.DivisionId,
                Tier = tier,
                CompetitionName = division.CompetitionName,
                Official = [row],
                Projected = [row]
            });

        // The form and the crowd come out of the same recent matches, so a test sets both by
        // setting the games. Every one of them won, because a form of anything else is a
        // different test's subject.
        var history = new List<TeamMatchRecord>();

        for (var game = 0; game < homeMatches; game++)
        {
            history.Add(new TeamMatchRecord
            {
                MatchId = Guid.NewGuid(),
                TeamId = teamId,
                IsHome = true,
                IsDivision = true,
                GoalsFor = 1,
                GoalsAgainst = 0,
                Attendance = (int)attendance
            });
        }

        matches
            .Setup(repo => repo.GetTeamHistoryByTeamsAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, int take, CancellationToken _) =>
                ids.ToDictionary(
                    id => id,
                    id => (IReadOnlyList<TeamMatchRecord>)(history
                        .Where(line => line.TeamId == id)
                        .Take(take)
                        .ToList())));

        cupTies
            .Setup(repo => repo.GetAliveClubsInCupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stillInCup ? [teamId] : []);

        return new World(
            new SponsorClubFactsService(competitions.Object, matches.Object, cupTies.Object, standings.Object),
            competitions);
    }
}