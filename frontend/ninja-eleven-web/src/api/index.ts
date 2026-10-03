import api from './client';
import type {
  SquadPlayerDto,
  SeasonCalendarDto,
  TeamMatchRecordDto,
  PlayerSeasonStateDto,
  TrainingResultDto,
  SquadTrainingQuotesDto,
  PlayerDto, CompetitionDto, CompetitionEditionDto, SeasonDto, TeamDto,
  CrestDto, CrestShape, KitDto,
  FixtureDto, NextFixtureDto, RoundDto, StandingDto, CompetitionStandingsDto, ScorerDto, LeagueSetupResult,
  MatchDto, MatchEventDto, MatchLineupDto, MatchStateDto, MatchContextDto, LiveMatchDto,
  MatchCommandResult, MatchEngineEventDto, MatchResult, RoundSimulationResult, TacticDto, Guid, MatchdayReportDto,
  PlayerProfileDto,
  ClubProfileDto,
  SquadSuggestionDto,
  FinanceLedgerDto,
  InboxBoxDto,
  InboxMessageDto,
  ClubScorerDto,
  CompetitionFilter,
  ClubStandingDto,
  CupBracketDto,
  CupPrizeDto,
  CupRulesDto,
  DivisionPurseDto,
  PyramidRulesDto,
  TopScorerPrizeListDto,
   SponsorOfferDto,
   SponsorBookDto,
   ManagerDto,
   TransferListingDto,
   TransferProposalDto,
   TransferInboxDto,
   TransferSearchResultDto,
   TransferSearchFilters,
   TransferHistoryLineDto,
   ReleaseResultDto,
  TacticsBoardDto,
  TacticsPlanDto,
    SaveTacticsPlanRequestDto,
    AcademyPlayerDto,
    PromoteAcademyResponseDto,
    TransferListResultDto,
    RenewContractRequestDto,
    RenewContractResponseDto,
    AuthResponseDto,
   AuthRegisterRequestDto,
   AuthLoginRequestDto,
    ChangePasswordRequestDto,
    UpdateTeamNameRequestDto,
    UpdateTeamColorsRequestDto,
  UpdateTeamCrestRequestDto,
  UpdateShirtNumberResponseDto,
  UpdateTeamKitsRequestDto,
    ClubRankingDto,
    ClubBalanceDto,
    DivisionRecentTransfersDto,
    ClubTransferHistoryDto,
    TransferRankingsDto,
  CrowdModuleDto,
  RivalDto,
  StadiumWorkStartedDto,
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
/**
 * The request that says "this club has no crest".
 *
 * The backend reads a crest with neither a letter nor a figure as a clearing rather than as a
 * refusal, so the editor can offer "sem escudo" without inventing a shape to send instead.
 */
const clearTheCrest = () => ({
  shape: 'Shield' as CrestShape,
  primaryColor: '#1f3c56',
  secondaryColor: '#f5f5f5',
  text: null,
  emblem: null,
});

export const TeamApi = {
  list: () => api.get<TeamDto[]>('/team').then(r => r.data),
  get: (id: string) => api.get<TeamDto>(`/team/${id}`).then(r => r.data),

  /**
   * The club's whole training sheet in one call: every man, every attribute, and what a
   * session on each costs. One call rather than one per player because a manager opening the
   * training screen is asking about twenty-three men at once — and because a price fetched
   * per man would put the first row's price and the last row's price from two different
   * moments, with the manager clicking through both.
   */
  training: (teamId: string, seasonId?: string) =>
    api
      .get<SquadTrainingQuotesDto>(
        `/team/${teamId}/training${seasonId ? `?seasonId=${seasonId}` : ''}`
      )
      .then(r => r.data),
  /**
   * Marks the club as the manager's own. The flag is what the world reads to decide who a
   * transfer belongs to — a proposal is only the manager's when the club is his — and it is
   * written by the career opening rather than inferred from a route or a cookie, so a refresh
   * does not turn a manager into a spectator.
   */
  takeOverAsManagerClub: (id: string) =>
    api.post<TeamDto>(`/team/${id}/manager-club`).then(r => r.data),
  getSquad: (teamId: string, seasonId: string) =>
    api.get<SquadPlayerDto[]>(`/team/${teamId}/squad/${seasonId}`).then(r => r.data),

  /**
   * The club's page, whole: who it is, who runs it, what it costs, its shelf and its history.
   *
   * One call because the page is one thing. This screen used to ask for the balance here, the
   * roster there and the rest of the club from a stand-in it drew itself, which meant the page
   * showed a club's size beside a club's balance from two different years and a history that
   * was invented — and invented *stably*, so two clubs never looked alike and no manager could
   * tell it from a page that worked.
   *
   * The season is optional and moves one number, the size of the roster: the balance is
   * deliberately not narrowed to a season and the history is the club's whole career.
   */
  getProfile: (teamId: string, seasonId?: string) =>
    api
      .get<ClubProfileDto>(
        `/team/${teamId}/profile` + (seasonId ? `?seasonId=${seasonId}` : '')
      )
      .then(r => r.data),
  /**
   * The club's place in a season: the division it is in and the line it holds there.
   *
   * Asked of the club rather than of a competition, because the club is the subject and the
   * season is what says which division — the same club is a different club in a different
   * division next year, and a division read off the club itself would survive a relegation.
   * The season is required for the same reason: without it the question has no answer.
   */
  getStanding: (teamId: string, seasonId: string) =>
    api.get<ClubStandingDto>(`/team/${teamId}/standing?seasonId=${seasonId}`).then(r => r.data),
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

  /**
   * The club's balance, read from the last line written in its whole book.
   * The market shows this above everything else — a manager bidding on a man
   * needs to know whether his club can pay.
   */
  getBalance: (teamId: string) =>
    api.get<ClubBalanceDto>(`/team/${teamId}/balance`).then(r => r.data),

  /** Changes the display name of the club the manager has taken charge of. */
  updateName: (id: string, name: string) =>
    api.put<TeamDto>(`/team/${id}/name`, { name } as UpdateTeamNameRequestDto).then(r => r.data),

  /** Changes the kit colours of the club the manager has taken charge of. */
  updateColors: (id: string, primaryColor: string, secondaryColor: string) =>
    api.put<TeamDto>(`/team/${id}/colors`, { primaryColor, secondaryColor } as UpdateTeamColorsRequestDto).then(r => r.data),

  /**
   * Draws the club's crest. A null crest clears the badge rather than leaving it half drawn:
   * the shield goes back to the initials placeholder, which is what a club that never had one
   * has always looked like.
   */
  updateCrest: (id: string, crest: CrestDto | null) =>
    api.put<TeamDto>(`/team/${id}/crest`, crest ?? clearTheCrest()).then(r => r.data),

  /**
   * Draws both shirts at once.
   *
   * The second shirt exists for exactly one reason — to be changed into when the first one
   * clashes — so a club that has decided what it will wear when it clashes has decided both.
   * A null away shirt is a club that has decided it has no second one.
   */
  updateKits: (id: string, homeKit: KitDto, awayKit: KitDto | null) =>
    api.put<TeamDto>(`/team/${id}/kits`, { homeKit, awayKit } as UpdateTeamKitsRequestDto).then(r => r.data),

  /**
   * Puts a man in a shirt, and answers with the number he ended up wearing.
   *
   * The answer is the backend's number rather than the one that was asked for, and the caller
   * paints what comes back: a client that trusted its own input would show a row saying 10
   * on a man the server had refused to give 10 to, and the refusal would only surface on the
   * next reload. The response carries one man rather than the whole squad because the screen
   * sending this already holds the squad and wants to change one cell of it.
   */
  updateShirtNumber: (id: string, playerId: string, shirtNumber: number) =>
    api
      .put<UpdateShirtNumberResponseDto>(`/team/${id}/shirt-number`, { playerId, shirtNumber })
      .then(r => r.data),

  /**
   * The club's youth academy players for a season: the list the Base screen reads from.
   */
  getAcademy: (teamId: string, seasonId: string) =>
    api
      .get<AcademyPlayerDto[]>(`/team/${teamId}/academy/${seasonId}`)
      .then(r => r.data),

  /** Promotes an academy player to the first team squad. */
  promoteAcademyPlayer: (teamId: string, playerId: string, seasonId: string) =>
    api
      .post<PromoteAcademyResponseDto>(
        `/team/${teamId}/academy/${playerId}/promote?seasonId=${seasonId}`
      )
      .then(r => r.data),

  /** Puts a squad player on the active transfer list. */
  putOnTransferList: (teamId: string, playerId: string, seasonId: string) =>
    api
      .post<TransferListResultDto>(`/team/${teamId}/transfer-list/${playerId}?seasonId=${seasonId}`)
      .then(r => r.data),

  /** Takes a squad player off the active transfer list. */
  takeOffTransferList: (teamId: string, playerId: string, seasonId: string) =>
    api
       .delete<TransferListResultDto>(`/team/${teamId}/transfer-list/${playerId}?seasonId=${seasonId}`)
       .then(r => r.data),

  /** Renews a player's contract. The wage is calculated by the backend; the client only sends the desired length. */
  renewContract: (teamId: string, request: RenewContractRequestDto) =>
    api
      .post<RenewContractResponseDto>(`/team/${teamId}/contract/renew`, request)
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
  /**
   * One training session: it spends energy and gets a point, and it is a POST because the
   * energy is gone whether or not the request arrives twice. The attribute is named rather
   * than numbered, and a name the game does not have is refused by the backend's own
   * ValidationFailed contract rather than by a check invented here.
   */
  train: (playerId: string, attribute: string, seasonId?: string) =>
    api
      .post<TrainingResultDto>(`/player/${playerId}/training`, {
        attribute,
        seasonId: seasonId ?? null,
      })
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
   * The cup's rules: how many clubs it is drawn from, how a tie is decided, and what the winner
   * goes on to play.
   *
   * Asked for rather than written on the screen, for the same reason the pyramid's rules are: the
   * bracket and the prize legend between them say which clubs are in a tie and what the run is
   * worth, and neither says that the tie is two matches, that the aggregate decides it, or that a
   * level aggregate goes to penalties. A client that restated those is a client promising a cup
   * the game does not play, and the failure is invisible until the constants move — at which
   * point the bracket it was explaining is still the bracket the game drew.
   */
  getCupRules: () => api.get<CupRulesDto>('/competition/cup-rules').then(r => r.data),

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
  // The club's next match in the order football is played. It is a backend question because
  // the order is a rule: a matchday runs Supercup, championship, cup, while a window's number
  // is only an identifier, and sorting a season's windows by it put a cup leg ahead of the
  // championship of the same day — a next match the server then refused to start.
  getNext: (teamId: string, seasonId: string) =>
    api
      .get<NextFixtureDto | null>(`/fixture/next?teamId=${teamId}&seasonId=${seasonId}`)
      .then(r => r.data ?? null),
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
   * The scoring chart, optionally restricted to one kind of competition and to one division.
   *
   * The competition is the backend's filter and not a column the client narrows: only the round
   * knows whether a tie was a division match, a cup tie or a Supercup, so a cup's chart counted
   * from the season total would be the league's goals wearing the cup's name.
   *
   * The division is the same walk one step further, and the artilharia needs it: the league is
   * four divisions of one kind, so a chart without one is a chart of the country — the first
   * division's own top scorer sitting below a fourth-division forward, on the first division's
   * page, next to the first division's money.
   */
  getScorers: (
    seasonId: string,
    topN = SCORER_POOL,
    competition?: CompetitionFilter,
    divisionId?: string | null,
  ) =>
    api.get<ScorerDto[]>(
      `/league/scorer/${seasonId}?topN=${topN}` +
        `${competition ? `&competition=${competition}` : ''}` +
        `${divisionId ? `&divisionId=${divisionId}` : ''}`
    ).then(r => r.data),

  /**
   * What each division's table is paid out of, and what a position in it is worth.
   *
   * The backend owns the split, because the last club is paid the rounding remainder of the
   * other eleven — a client dividing the purse by twelve publishes a championship that does not
   * add up.
   */
  getPrizes: () => api.get<DivisionPurseDto[]>('/league/prizes').then(r => r.data),

  /**
   * The pyramid's rules: the order a table is settled in, and what each division's table ends
   * the season with.
   *
   * It is asked of the backend because both of those are the engine's. The chain is the chain
   * the sort walks and the bands are the movement the close of the season makes, so a screen
   * that said "os 4 primeiros sobem" out of a constant of its own would be promising four
   * accesses to a champion who has no division above him.
   */
  getRules: () => api.get<PyramidRulesDto>('/league/rules').then(r => r.data),
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
  // The match a club is playing right now, or null when it is not playing one. It is what
  // a navigation badge is drawn from, so a manager finds out his club is mid-game from
  // every screen and not only from the one he remembered to open.
  getLiveForTeam: (teamId: string) =>
    api.get<LiveMatchDto | null>(`/match/live/${teamId}`).then(r => r.data ?? null),
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
  /**
   * Signs a new shirt deal. Fails if the current one is still active.
   *
   * The fee and the length are not sent, and there is nowhere to send them to: both are the
   * sponsor's to decide and both are on the offer the screen was already showing. A client
   * that could name a price would be quoting a number the book had never agreed to.
   */
  sign: (teamId: string, seasonId: string, sponsorId: string) =>
    api
      .post<SponsorBookDto>(`/team/${teamId}/sponsor/sign?seasonId=${seasonId}`, { sponsorId })
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

export const TransferApi = {
  /**
   * The market, narrowed by whatever the manager set and paginated.
   *
   * Only the filters that are actually set are sent, because only the filters that are set are
   * meant to exclude anybody: a market that loaded a different list of men for each combination
   * of boxes is a market that disagrees with itself, and a manager cannot tell a filter from a
   * bug. The order is the backend's own fixed shuffle, so page two is the same page two twice.
   */
  search: (
    seasonId: string,
    filters: TransferSearchFilters = {},
    page = 1,
    pageSize = 30
  ) => {
    const params = new URLSearchParams({ seasonId, page: String(page), pageSize: String(pageSize) });

    const set = (name: string, value?: number | boolean | string) => {
      if (value === undefined || value === '') return;
      params.set(name, String(value));
    };

    set('position', filters.position);
    set('minAge', filters.minAge);
    set('maxAge', filters.maxAge);
    set('minStars', filters.minStars);
    set('maxStars', filters.maxStars);
    set('minSpeed', filters.minSpeed);
    set('minAccuracy', filters.minAccuracy);
    set('minDribbling', filters.minDribbling);
    set('minHeading', filters.minHeading);
    set('minStrength', filters.minStrength);
    set('minGoalkeeperPower', filters.minGoalkeeperPower);
    set('minReflexes', filters.minReflexes);
    set('retiring', filters.retiring);
    set('freeAgentsOnly', filters.freeAgentsOnly);
    set('withClubOnly', filters.withClubOnly);
    set('teamId', filters.teamId);

    return api.get<TransferSearchResultDto>(`/transfer/search?${params.toString()}`).then(r => r.data);
  },

  getListing: (playerId: string, seasonId: string) =>
    api.get<TransferListingDto>(`/transfer/listing/${playerId}?seasonId=${seasonId}`).then(r => r.data),

  getInbox: (clubId: string, seasonId: string) =>
    api.get<TransferInboxDto>(`/transfer/inbox/${clubId}?seasonId=${seasonId}`).then(r => r.data),

  getHistory: (playerId: string) =>
    api.get<TransferHistoryLineDto[]>(`/transfer/history/${playerId}`).then(r => r.data),

  /**
   * The transfers that finished in the last few rounds, across every club of the division
   * the manager's own club plays in.
   */
  getRecent: (clubId: string, windowRounds = 3) =>
    api.get<DivisionRecentTransfersDto>(`/transfer/recent?clubId=${clubId}&windowRounds=${windowRounds}`).then(r => r.data),

  /**
   * The four transfer rankings of the division: most players bought, most sold, most spent,
   * and most profit. Profit is net — fees received minus fees paid.
   */
  getRankings: (clubId: string) =>
    api.get<TransferRankingsDto>(`/transfer/rankings?clubId=${clubId}`).then(r => r.data),

  /**
   * Every transfer involving one club, across the seasons given, newest first.
   * Pending and accepted sit in the same table as completed ones.
   */
  getClubHistory: (teamId: string, seasonNumbers: number[]) =>
    api.get<ClubTransferHistoryDto>(`/transfer/club/${teamId}/history`, { params: { seasonNumbers } }).then(r => r.data),

  /**
   * Offers a player to a club. The fee is optional: without it the asking price is offered, and
   * a player with no club costs a signing fee rather than a price, because there is nobody to
   * buy him from.
   */
  propose: (playerId: string, buyingClubId: string, fee?: number) =>
    api.post<TransferProposalDto>('/transfer/propose', { playerId, buyingClubId, fee }).then(r => r.data),

  /**
   * Answers an offer addressed to this club. The club is named in the answer and checked by the
   * backend against the seller: only the club holding the player can decide his price, and a
   * buyer accepting its own offer would be writing its own cheque.
   */
  answer: (transferId: string, clubId: string, accept: boolean) =>
    api.post<TransferProposalDto>(`/transfer/${transferId}/answer`, { clubId, accept }).then(r => r.data),

    release: (playerId: string, clubId: string) =>
    api.post<ReleaseResultDto>(`/transfer/${playerId}/release?clubId=${clubId}`).then(r => r.data),

    /** Deals young free agents onto the market: sixteen to nineteen, no club, no contract. */
    seedYoungPlayers: (count = 88) =>
    api.post(`/transfer/seed-young-players?count=${count}`).then(r => r.data),
};

export const AuthApi = {
  /** Registers a new account. Club is auto-assigned by the backend. */
  register: (request: AuthRegisterRequestDto) =>
    api.post<AuthResponseDto>('/auth/register', request).then(r => r.data),

  login: (request: AuthLoginRequestDto) =>
    api.post<AuthResponseDto>('/auth/login', request).then(r => r.data),

  /** Changes the authenticated user's password. */
  changePassword: (request: ChangePasswordRequestDto) =>
    api.put('/auth/password', request).then(r => r.data),
};

export const RankingApi = {
  /** Gets the Ninja Ranking for all clubs. */
  getRanking: () => api.get<ClubRankingDto[]>('/ranking').then(r => r.data),
};

/**
 * The manager's box.
 *
 * It is read-only in the way the game is: nothing here composes a message and nothing here
 * takes one. The only verbs are reading a page and opening a line, because the messages are
 * written by the engine and the manager's part in the box is to read it.
 */
export const InboxApi = {
  /**
 * A page of the club's box, newest first, with the number of unread messages beside it.
 *
 * The category is sent as the backend's own name and the filter is applied **by the server**,
 * on the page, on the count and on the paging at once. Filtering the twenty lines on screen
 * instead would answer "how much of my mail is this" with the twenty lines the screen happened
 * to be holding, and a manager would page through his own box hunting for the rest of what one
 * button said was there.
 */
getBox: (teamId: string, page = 1, pageSize = 20, category?: string | null) =>
  api
    .get<InboxBoxDto>(`/inbox/${teamId}`, {
      params: { page, pageSize, ...(category ? { category } : {}) }
    })
    .then(r => r.data),

  /** How many messages the manager has not opened. The number on the column. */
  getUnreadCount: (teamId: string) =>
    api.get<number>(`/inbox/${teamId}/unread`).then(r => r.data),

  /**
   * Opens a message. The club is in the route and the backend checks the line belongs to it,
   * so a message id guessed by hand is refused rather than somebody else's mail.
   */
  markRead: (teamId: string, messageId: string) =>
    api.post<InboxMessageDto>(`/inbox/${teamId}/read/${messageId}`).then(r => r.data),

  /** Empties the unread badge in one go, for a manager who has caught up. */
  markAllRead: (teamId: string) =>
    api.post<number>(`/inbox/${teamId}/read-all`).then(r => r.data),
};

/**
 * The board a manager lays his club's next match out on, and the order he leaves on it.
 *
 * It is its own client and not part of the match one because the kick-off is no longer
 * something a manager opens: the world's schedule opens a match, so what a manager writes
 * here has to be somewhere the match can find without him.
 */
export const TacticsApi = {
  /**
   * The fixture, the opponent, the squad and the order left on it — in one call, because a
   * board assembled from three requests could show a squad that disagrees with the one the
   * kick-off is about to use.
   */
  board: (teamId: string, seasonId: string) =>
    api
      .get<TacticsBoardDto>('/tactics/board', { params: { teamId, seasonId } })
      .then(r => r.data),

  /**
   * Writes the order down. It is a POST because what is stored is the manager's decision and
   * the moment he took it — a plan written twice is the last decision, not the merge of two.
   */
  savePlan: (request: SaveTacticsPlanRequestDto) =>
    api.post<TacticsPlanDto>('/tactics/plan', request).then(r => r.data),
};

/**
 * The club's supporters, its ground and its rivals.
 *
 * One call because the screen is one page. The crowd, the ground, the building site and the
 * rivals are four different reads in the backend and one thing to a manager — and a page that
 * draws each as it lands is a page showing a crowd from one year beside a ground from another.
 */
export const CrowdApi = {
  /**
   * The whole module, and null is not a shape this returns: a club that is not there comes back
   * as a 404 with a code, because a club that does not exist and a club with no crowd yet are
   * different answers and collapsing them would send a manager looking for a crowd that was
   * never going to be written.
   */
  module: (teamId: string) =>
    api.get<CrowdModuleDto>(`/team/${teamId}/crowd`).then(r => r.data),

  /**
   * The four rivals on their own. The module already carries them; this is for a screen that
   * wants the rivals and nothing else.
   */
  rivals: (teamId: string) =>
    api.get<RivalDto[]>(`/team/${teamId}/rivals`).then(r => r.data),

  /**
   * Asks for a project on the club's ground.
   *
   * The request carries the number of seats and nothing else. The cost, the rounds and the
   * project itself are the catalogue's answer — a screen that could name a price would be naming
   * a number the book had never agreed to, and a manager budgeting his season against it would
   * be budgeting against a lie.
   *
   * Asking twice is not an error: a ground that is already building answers with the open
   * project rather than a refusal, which is the friendly reading of the same fact the unique
   * index on the open project enforces.
   */
  startExpansion: (teamId: string, seats: number, playedThroughRound: number) =>
    api
      .post<StadiumWorkStartedDto>(`/team/${teamId}/stadium/expansion`, { seats }, {
        params: { playedThroughRound },
      })
      .then(r => r.data),
};
