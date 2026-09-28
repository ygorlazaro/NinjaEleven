import api from './client';
import type {
  SquadPlayerDto,
  SeasonCalendarDto,
  TeamMatchRecordDto,
  PlayerSeasonStateDto,
  PlayerDto, CompetitionDto, CompetitionEditionDto, SeasonDto, TeamDto,
  FixtureDto, RoundDto, StandingDto, CompetitionStandingsDto, ScorerDto, LeagueSetupResult,
  MatchDto, MatchEventDto, MatchLineupDto, MatchStateDto, MatchContextDto,
  MatchCommandResult, MatchEngineEventDto, MatchResult, RoundSimulationResult, TacticDto, Guid, MatchdayReportDto,
  PlayerProfileDto,
  SquadSuggestionDto,
  FinanceLedgerDto,
  ClubScorerDto,
  CompetitionFilter,
  CupBracketDto,
  CupPrizeDto,
  DivisionPurseDto,
  TopScorerPrizeListDto,
   SponsorOfferDto,
   SponsorBookDto,
   ManagerDto
} from '../types';

/**
 * How many scorers the chart asks for. It is the size of a whole league squad, so the
 * league's own list is complete before anything is filtered out of it.
 */
const SCORER_POOL = 250;

/**
 * Every route is singular, as the backend contract requires: /team, /player,
 * /competition, /season, /round, /fixture, /match, /league.
 */
export const TeamApi = {
  list: () => api.get<TeamDto[]>('/team').then(r => r.data),
  get: (id: string) => api.get<TeamDto>(`/team/${id}`).then(r => r.data),
  getSquad: (teamId: string, seasonId: string) =>
    api.get<SquadPlayerDto[]>(`/team/${teamId}/squad/${seasonId}`).then(r => r.data),
  /** The club's last finished matches, newest first, for the form guide on its card. */
  getMatches: (teamId: string, limit = 10) =>
    api.get<TeamMatchRecordDto[]>(`/team/${teamId}/matches?limit=${limit}`).then(r => r.data),

  /** Head-to-head matches between two clubs, newest first. */
  getHeadToHead: (teamId: string, opponentId: string, limit = 5) =>
    api.get<TeamMatchRecordDto[]>(`/team/${teamId}/head-to-head/${opponentId}?limit=${limit}`).then(r => r.data),
  /**
   * The club's scorers of a season, optionally for one kind of competition.
   *
   * The competition is the world's own name on the wire, and the season is required: goals
   * belong to a season and a table of scorers with no season is a table of everything, which
   * is a different question and a much slower one. The list comes whole, because a club's
   * scorers is a page about its history and a list that stopped at fifteen would be a list of
   * the men the endpoint felt like sending.
   */
  getScorers: (teamId: string, seasonId: string, competition?: CompetitionFilter, topN = 100) =>
    api
      .get<ClubScorerDto[]>(
        `/team/${teamId}/scorer?seasonId=${seasonId}&topN=${topN}` +
          (competition ? `&competition=${competition}` : '')
      )
      .then(r => r.data),
  /**
   * A page of the club's book and the three numbers above it.
   *
   * The page is asked for by number and not by skipping lines, because a ledger that is
   * skipped through can be read wrong: a line that arrives is a line the manager can see, and
   * a gap where one should be would be the screen's doing rather than the club's. The season
   * is optional because a career's books are a long read and a manager asking how last season
   * went is asking about one of them.
   */
  getFinance: (teamId: string, seasonId?: string, page = 1, pageSize = 10) =>
    api
      .get<FinanceLedgerDto>(
        `/team/${teamId}/finance?page=${page}&pageSize=${pageSize}` +
          (seasonId ? `&seasonId=${seasonId}` : '')
      )
      .then(r => r.data),
};

export const PlayerApi = {
  list: () => api.get<PlayerDto[]>('/player').then(r => r.data),
  get: (id: string) => api.get<PlayerDto>(`/player/${id}`).then(r => r.data),
  getSeasonState: (playerId: string, seasonId: string) =>
    api.get<PlayerSeasonStateDto>(`/player/${playerId}/season/${seasonId}`).then(r => r.data),
  // A player whole, in one call. The season is optional: without it the career still comes
  // back, because "how has this striker done" is a question about more than one season.
  getProfile: (playerId: string, seasonId?: string) =>
    api
      .get<PlayerProfileDto>(
        `/player/${playerId}/profile${seasonId ? `?seasonId=${seasonId}` : ''}`
      )
      .then(r => r.data),
};

export const CompetitionApi = {
  list: () => api.get<CompetitionDto[]>('/competition').then(r => r.data),

  /**
   * The editions running in a season — the three divisions of the championship, the cup and
   * the Supercup — rather than the competitions themselves. "Campeonato Brasileiro" runs three
   * times a season, once per tier, so a list of competitions cannot say which of the three a
   * table belongs to.
   */
  listEditionsBySeason: (seasonId: string) =>
    api.get<CompetitionEditionDto[]>(`/competition/by-season/${seasonId}`).then(r => r.data),

  /**
   * The clubs entered in one edition. A club's division is a fact about this list and not
   * about the club, so it is asked for rather than worked out from two other lists.
   */
  listClubs: (competitionSeasonId: string) =>
    api.get<TeamDto[]>(`/competition/${competitionSeasonId}/club`).then(r => r.data),

  get: (id: string) => api.get<CompetitionDto>(`/competition/${id}`).then(r => r.data),

  /**
   * The cup's bracket, with the rounds that have been drawn.
   *
   * The aggregate arrives per club and the two legs arrive as they were played, because the
   * legs swap ends: a client that added the two columns as they came would put a club's second
   * leg on the wrong side of the tie.
   */
  getBracket: (competitionSeasonId: string) =>
    api.get<CupBracketDto>(`/competition/${competitionSeasonId}/bracket`).then(r => r.data),

  /**
   * What the cup pays: the winner's cheque and the consolation for the round a club went out in.
   *
   * A knockout is paid on the way out, so the consolation is most of the list — and it is
   * backend-owned for the same reason the championship's shares are.
   */
  getCupPrizes: () => api.get<CupPrizeDto[]>('/competition/cup-prizes').then(r => r.data),

  /**
   * What an edition pays its artilharia, and who is holding the three places.
   *
   * Asked of an edition rather than of a season, because a season's championship is three
   * editions with three artilharias and three titles: one answer for the season would be a list
   * of the whole country's top scorers with no way to say which title pays which of them. A cup
   * is one edition, and the same call answers it — with each scorer carrying the division whose
   * title paid him.
   */
  getTopScorerPrizes: (competitionSeasonId: string) =>
    api
      .get<TopScorerPrizeListDto>(`/competition/${competitionSeasonId}/top-scorer-prize`)
      .then(r => r.data),
};

export const SeasonApi = {
  list: () => api.get<SeasonDto[]>('/season').then(r => r.data),
  current: () => api.get<SeasonDto>('/season/current').then(r => r.data),
  get: (id: string) => api.get<SeasonDto>(`/season/${id}`).then(r => r.data),
  /**
   * The season's matchdays and the windows of football scheduled on them. It is a GET that
   * draws the calendar when the season has none, because the draw is idempotent: asking
   * again gives back the calendar that is already there rather than a second one.
   */
  getCalendar: (id: string, build = true) =>
    api.get<SeasonCalendarDto>(`/season/${id}/calendar?build=${build}`).then(r => r.data),
};

export const RoundApi = {
  list: () => api.get<RoundDto[]>('/round').then(r => r.data),
  listByCompetitionSeason: (competitionSeasonId: string) =>
    api.get<RoundDto[]>(`/round/by-competition-season/${competitionSeasonId}`).then(r => r.data),
  get: (id: string) => api.get<RoundDto>(`/round/${id}`).then(r => r.data),
  // Plays every fixture of the round that is still scheduled, without a live session.
  // The round is over when this resolves, so the next one becomes the current one.
  simulate: (id: string) =>
    api.post<RoundSimulationResult>(`/round/${id}/simulate`).then(r => r.data),
};

export const FixtureApi = {
  list: () => api.get<FixtureDto[]>('/fixture').then(r => r.data),
  listByRound: (roundId: string) =>
    api.get<FixtureDto[]>(`/fixture/by-round/${roundId}`).then(r => r.data),
  get: (id: string) => api.get<FixtureDto>(`/fixture/${id}`).then(r => r.data),
};

export const LeagueApi = {
  setup: (competitionId: string, seasonId: string, teamIds: string[]) =>
    api.post<LeagueSetupResult>('/league/setup', { competitionId, seasonId, teamIds })
      .then(r => r.data),
  /**
   * The table of a competition edition, and the table it would be with the games in progress
   * counted in. Both are worked out by the backend and neither is sorted here.
   */
  getStandings: (compSeasonId: string) =>
    api.get<CompetitionStandingsDto>(`/league/standing/${compSeasonId}`).then(r => r.data),
  getStanding: (compSeasonId: string, teamId: string) =>
    api.get<StandingDto>(`/league/standing/${compSeasonId}/team/${teamId}`).then(r => r.data),
  /**
   * The whole scoring chart, not a global top 15: the league screen filters it down to the
   * manager's own club, and a club's scorers are missing from a global top 15 long before
   * the league's best forward is.
   */
  /**
   * The scoring chart, optionally restricted to one kind of competition.
   *
   * The competition is the backend's filter and not a column the client narrows: only the round
   * knows whether a tie was a division match, a cup tie or a Supercup, so a cup's chart counted
   * from the season total would be the league's goals wearing the cup's name.
   */
  getScorers: (seasonId: string, topN = SCORER_POOL, competition?: CompetitionFilter) =>
    api.get<ScorerDto[]>(
      `/league/scorer/${seasonId}?topN=${topN}${competition ? `&competition=${competition}` : ''}`
    ).then(r => r.data),

  /**
   * What each division's table is paid out of, and what a position in it is worth.
   *
   * The backend owns the split, because the last club is paid the rounding remainder of the
   * other eleven — a client dividing the purse by twelve publishes a championship that does not
   * add up.
   */
  getPrizes: () => api.get<DivisionPurseDto[]>('/league/prizes').then(r => r.data),
};

export const MatchApi = {
  list: () => api.get<MatchDto[]>('/match').then(r => r.data),
  get: (id: string) => api.get<MatchDto>(`/match/${id}`).then(r => r.data),
  getEvents: (id: string, afterSequence?: number) =>
    api.get<MatchEventDto[]>(
      `/match/${id}/event${afterSequence === undefined ? '' : `?afterSequence=${afterSequence}`}`
    ).then(r => r.data),

  // The commands drive the match engine on the backend. The engine returns the
  // matchId that every later call is keyed by; the feed itself arrives over SignalR.
  // starterIds is the eleven chosen on the lineup screen; benchIds is the bench;
  // the backend validates both and refuses an illegal one. tacticCode is the shape
  // the manager ordered — it decides which men the staff fill each line with.
  start: (fixtureId: string, userTeamId?: string, starterIds?: string[], benchIds?: string[], tacticCode?: string) =>
    api.post<MatchCommandResult>(`/match/start/${fixtureId}`, { userTeamId, starterIds, benchIds, tacticCode })
      .then(r => r.data),
  // The tactics the catalogue offers, and the eleven the staff would pick for one of
  // them. Both come from the backend so the screen is not the place the shape is
  // worked out twice.
  getTactics: () => api.get<TacticDto[]>('/match/tactics').then(r => r.data),
  // The round told back: scorelines and, for each match, the account the match gave of
  // itself. The league screen reads it rather than inventing one.
  getRoundReport: (roundId: string) =>
    api.get<MatchdayReportDto>(`/match/round-report/${roundId}`).then(r => r.data),
  getSuggestedEleven: (teamId: string, seasonId: string, tacticCode?: string) =>
    api
      .get<SquadSuggestionDto>(
        `/match/squad-suggestion?teamId=${teamId}&seasonId=${seasonId}` +
          (tacticCode ? `&tacticCode=${encodeURIComponent(tacticCode)}` : '')
      )
      .then(r => r.data),
  getHeadToHead: (teamId: string, opponentId: string, limit = 5) =>
    api
      .get<TeamMatchRecordDto[]>(`/match/head-to-head?teamId=${teamId}&opponentId=${opponentId}&limit=${limit}`)
      .then(r => r.data),
  // Plays a fixture to full time with nobody watching, for the matches of the league
  // the manager is not in.
  simulate: (fixtureId: string) =>
    api.post<MatchCommandResult>(`/match/simulate/${fixtureId}`).then(r => r.data),
  getLineup: (matchId: string, userTeamId?: string) =>
    api.get<MatchLineupDto>(
      `/match/lineup/${matchId}${userTeamId ? `?userTeamId=${userTeamId}` : ''}`
    ).then(r => r.data),
  getState: (matchId: string) =>
    api.get<MatchStateDto>(`/match/state/${matchId}`).then(r => r.data),
  // Where the match is and what it is: season, day, competition, phase, ground, and the
  // other leg of a cup tie. The header reads from this and never works it out.
  getContext: (matchId: string) =>
    api.get<MatchContextDto>(`/match/context/${matchId}`).then(r => r.data),
  tick: (matchId: string) =>
    api.post<MatchEngineEventDto[]>(`/match/tick/${matchId}`).then(r => r.data),
  pause: (matchId: string) =>
    api.post<MatchCommandResult>(`/match/pause/${matchId}`).then(r => r.data),
  resume: (matchId: string) =>
    api.post<MatchCommandResult>(`/match/resume/${matchId}`).then(r => r.data),
  changeSpeed: (matchId: string, speed: number) =>
    api.post<MatchCommandResult>(`/match/speed/${matchId}/${speed}`).then(r => r.data),
  continueSecondHalf: (matchId: string) =>
    api.post<MatchCommandResult>(`/match/continue-second-half/${matchId}`).then(r => r.data),
  substitute: (matchId: string, teamId: string, playerOut: string, playerIn: string) =>
    api.post<MatchCommandResult>(`/match/substitution/${matchId}/team/${teamId}`, { playerOutId: playerOut, playerInId: playerIn })
      .then(r => r.data),
  selectPenaltyTaker: (matchId: string, teamId: string, playerId: string) =>
    api.post<MatchCommandResult>(`/match/penalty-taker/${matchId}/team/${teamId}`, { playerId })
      .then(r => r.data),
  /**
   * Names the order the club will take a shootout in. It is a list because a shootout is
   * five kicks and the order is the manager's; the backend refuses a man who may not take
   * and a man named twice, so a client cannot offer either.
   */
  nameShootoutOrder: (matchId: string, teamId: string, takerIds: string[]) =>
    api.post<MatchCommandResult>(`/match/shootout-order/${matchId}/team/${teamId}`, { takerIds })
      .then(r => r.data),
  getResult: (matchId: string) =>
    api.get<MatchResult>(`/match/result/${matchId}`).then(r => r.data),
};

export const SponsorApi = {
  /**
   * The sponsor book of a club for a season: the deal on the shirt, how many matches
   * are left, and the offers waiting for the manager to choose.
   */
  getBook: (teamId: string, seasonId: string) =>
    api.get<SponsorBookDto>(`/team/${teamId}/sponsor?seasonId=${seasonId}`).then(r => r.data),
  /** Signs a new shirt deal. Fails if the current one is still active. */
  sign: (teamId: string, seasonId: string, sponsorId: string, perMatchFee?: number, contractMatches?: number) =>
    api
      .post<SponsorBookDto>(`/team/${teamId}/sponsor/sign?seasonId=${seasonId}`, {
        sponsorId,
        perMatchFee,
        contractMatches
      })
      .then(r => r.data),
};

export const ManagerApi = {
  /** The manager of a club, if the career has begun. */
  getByTeam: (teamId: string) =>
    api.get<ManagerDto>(`/team/${teamId}/manager`).then(r => r.data),
  /** Creates the manager and starts the career. A club already managed is refused. */
  create: (teamId: string, name: string) =>
    api.post<ManagerDto>(`/team/${teamId}/manager`, { name }).then(r => r.data),
  /** Changes the manager's name. */
  rename: (teamId: string, name: string) =>
    api.put<ManagerDto>(`/team/${teamId}/manager`, { name }).then(r => r.data),
};
