using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Competitions;

namespace NinjaEleven.Application.Repositories;

public interface ICompetitionRepository
{
    Task<IReadOnlyList<Competition>> ListAsync(CancellationToken cancellationToken = default);
    Task<Competition?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Competition competition, CancellationToken cancellationToken = default);
    void Update(Competition competition);

    /// <summary>
    /// Competitions that took part in a given season.
    /// </summary>
    Task<IReadOnlyList<Competition>> ListBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task<CompetitionSeason?> GetSeasonAsync(
        Guid competitionId,
        Guid seasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One tier's edition of a competition. A competition can run more than once in a season,
    /// so a season is not enough to say which edition is meant.
    /// </summary>
    Task<CompetitionSeason?> GetSeasonForDivisionAsync(
        Guid competitionId,
        Guid seasonId,
        Guid divisionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One edition of a competition by its own identifier. Resolving a fixture to the
    /// season it belongs to goes through the round and the edition.
    /// </summary>
    Task<CompetitionSeason?> GetSeasonByIdAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One edition with the kind of competition it is and the tier it is the table of.
    /// </summary>
    Task<CompetitionSeasonView?> GetSeasonViewByIdAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every edition of every competition in a season, ordered so the divisions come first by
    /// tier and the knockouts after them. The calendar needs to know that order and a caller
    /// that has to sort it has been handed structure it should have been given.
    /// </summary>
    Task<IReadOnlyList<CompetitionSeasonView>> ListSeasonViewsAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default);

    Task AddSeasonAsync(CompetitionSeason competitionSeason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompetitionParticipant>> ListParticipantsAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    Task AddParticipantsAsync(
        IEnumerable<CompetitionParticipant> participants,
        CancellationToken cancellationToken = default);
}
