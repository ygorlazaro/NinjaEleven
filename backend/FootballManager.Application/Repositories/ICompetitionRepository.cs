using FootballManager.Domain.Competitions;

namespace FootballManager.Application.Repositories;

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
    /// One edition of a competition by its own identifier. Resolving a fixture to the
    /// season it belongs to goes through the round and the edition.
    /// </summary>
    Task<CompetitionSeason?> GetSeasonByIdAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    Task AddSeasonAsync(CompetitionSeason competitionSeason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompetitionParticipant>> ListParticipantsAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default);

    Task AddParticipantsAsync(
        IEnumerable<CompetitionParticipant> participants,
        CancellationToken cancellationToken = default);
}
