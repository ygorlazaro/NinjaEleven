using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Application.Services;

public class CompetitionService
{
    private readonly ICompetitionRepository _competitionRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompetitionService(
        ICompetitionRepository competitionRepository,
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        IUnitOfWork unitOfWork)
    {
        _competitionRepository = competitionRepository;
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Competition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _competitionRepository.ListAsync(cancellationToken);

    public async Task<Competition> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _competitionRepository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Competition", id);

    /// <summary>
    /// The competitions running in a season, and which edition of each one.
    ///
    /// These are the editions rather than the competitions, because the competitions on their
    /// own cannot address the world: "Campeonato Brasileiro" runs three times in a season, once
    /// per tier, and a list of competitions has nowhere to say which of the three a club is in.
    /// An edition is the thing a table, a fixture and a trophy all belong to, so it is the
    /// thing that is named here.
    /// </summary>
    public async Task<IReadOnlyList<CompetitionSeasonView>> GetBySeasonAsync(
        Guid seasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _seasonRepository.GetAsync(seasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Season", seasonId);
        }

        return await _competitionRepository.ListSeasonViewsAsync(seasonId, cancellationToken);
    }

    /// <summary>
    /// The clubs entered in one edition of a competition, with their grounds.
    ///
    /// A club is not in a division: it is entered in one edition of it. The division is a
    /// property of the edition and the club's place in it is a property of the enrolment, so
    /// "which clubs are in the 1ª Divisão" is a question about the edition and not one a
    /// client can answer by reading a list of clubs and a list of divisions and joining them
    /// up itself.
    /// </summary>
    public async Task<IReadOnlyList<Team>> GetClubsAsync(
        Guid competitionSeasonId,
        CancellationToken cancellationToken = default)
    {
        if (await _competitionRepository.GetSeasonViewByIdAsync(competitionSeasonId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("CompetitionSeason", competitionSeasonId);
        }

        var participants = await _competitionRepository.ListParticipantsAsync(
            competitionSeasonId, cancellationToken);

        if (participants.Count == 0)
        {
            return Array.Empty<Team>();
        }

        var clubs = await _teamRepository.ListByIdsAsync(
            participants.Select(participant => participant.TeamId), cancellationToken);

        return clubs;
    }

    public async Task<Competition> CreateAsync(
        string name,
        CompetitionType type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("CompetitionNameRequired", "O nome da competição é obrigatório.");
        }

        var competition = Competition.Create(name.Trim(), type);
        await _competitionRepository.AddAsync(competition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return competition;
    }
}
