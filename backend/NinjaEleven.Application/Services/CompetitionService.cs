using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Application.Models;
using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Finance;
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

    /// <summary>
    /// What a cup run is paid: the consolation for the round a club went out in, and the
    /// winner's cheque.
    ///
    /// It is asked for the same reason the championship purses are: a knockout pays on the way
    /// out, so a screen that listed the winner and forgot the loser would tell a manager what
    /// his club is playing for and not what it is playing against — and a first-round loser
    /// being paid three hundredth of the finalist is the difference between a cup where the run
    /// mattered and one where it did not.
    /// </summary>
    public IReadOnlyList<CupPrize> GetCupPrizes()
    {
        var prizes = new List<CupPrize>
        {
            // The champion's cheque is the biggest number in the competition and it is not a
            // consolation, so it carries no round of its own and says so in words: a line
            // headed "final" beside the line for the club that lost the final is a legend that
            // has to be decoded before it can be read.
            new(0, "Campeão", PrizeRules.CupChampionPrize, true)
        };

        for (var tieRound = 1; tieRound <= CompetitionRules.CupRounds; tieRound++)
        {
            prizes.Add(new CupPrize(
                tieRound,
                CompetitionRules.TieRoundName(tieRound),
                PrizeRules.CupConsolation(tieRound),
                false,
                // The last tie-round is the final, and a club that loses it did not go out in
                // the final — it is the runner-up, and it is paid as the second half of a result
                // rather than as the consolation for going home. The round that says so is
                // PrizeRules' own rather than the loop's, because the ceremony that pays the
                // champion reads the same constant and a legend that worked it out a second way
                // is a legend that one day calls the final the semifinal.
                tieRound == PrizeRules.RunnerUpTieRound));
        }

        return prizes;
    }

    /// <summary>
    /// The cup's own rules: how many clubs it is drawn from, how a tie is decided, and what the
    /// winner goes on to play.
    ///
    /// It is asked for the same reason the pyramid's rules are, and the reason is the one that
    /// matters for a knockout: the bracket already says which two clubs are in a tie, and the
    /// prize legend already says what the run is worth, and between those two nothing says that a
    /// tie is two matches, that the aggregate decides it, or that a level aggregate goes to
    /// penalties. A manager planning a tie he expects to be level cannot plan it from a bracket.
    ///
    /// Nothing here is a second copy — every number is read from <c>CompetitionRules</c> — so a
    /// screen printing "são jogos de ida e volta" out of a constant of its own would be a screen
    /// that is wrong the day the cup stops being two-legged, and the bracket it was explaining
    /// would still be the bracket the game drew.
    /// </summary>
    public CupRuleSet GetCupRules() => new(
        CupRules.Size,
        CupRules.TieRounds,
        CupRules.LegsPerTie,
        CupRules.AllowsExtraTime,
        CupRules.AggregateRule,
        CupRules.LevelTieRule,
        CupRules.WinnerTakesRule,
        CupRules.Rounds()
            .Select(round => new CupRoundRuleLine(
                round.TieRound,
                round.Name,
                round.ClubsIn,
                round.Ties,
                round.FirstLegDay,
                round.SecondLegDay))
            .ToList());

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
