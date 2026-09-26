import api from './client';
import type {
  SquadPlayerDto,
  PlayerSeasonStateDto,
  PlayerDto, CompetitionDto, SeasonDto, TeamDto,
  FixtureDto, RoundDto, StandingDto, ScorerDto, LeagueSetupResult,
  MatchDto, MatchEventDto, MatchLineupDto, MatchStateDto,
  MatchCommandResult, MatchEngineEventDto, MatchResult
} from '../types';

/**
 * Every route is singular, as the backend contract requires: /team, /player,
 * /competition, /season, /round, /fixture, /match, /league.
 */
export const PlayerApi = {
  list: () => api.get<PlayerDto[]>('/player').then(r => r.data),
  get: (id: string) => api.get<PlayerDto>(`/player/${id}`).then(r => r.data),
  getSeasonState: (playerId: string, seasonId: string) =>
    api.get<PlayerSeasonStateDto>(`/player/${playerId}/season/${seasonId}`).then(r => r.data),
};

export const TeamApi = {
  list: () => api.get<TeamDto[]>('/team').then(r => r.data),
  get: (id: string) => api.get<TeamDto>(`/team/${id}`).then(r => r.data),
  getSquad: (teamId: string, seasonId: string) =>
    api.get<SquadPlayerDto[]>(`/team/${teamId}/squad/${seasonId}`).then(r => r.data),
};

export const CompetitionApi = {
  list: () => api.get<CompetitionDto[]>('/competition').then(r => r.data),
  listBySeason: (seasonId: string) =>
    api.get<CompetitionDto[]>(`/competition/by-season/${seasonId}`).then(r => r.data),
  get: (id: string) => api.get<CompetitionDto>(`/competition/${id}`).then(r => r.data),
};

export const SeasonApi = {
  list: () => api.get<SeasonDto[]>('/season').then(r => r.data),
  current: () => api.get<SeasonDto>('/season/current').then(r => r.data),
  get: (id: string) => api.get<SeasonDto>(`/season/${id}`).then(r => r.data),
};

export const RoundApi = {
  list: () => api.get<RoundDto[]>('/round').then(r => r.data),
  listByCompetitionSeason: (competitionSeasonId: string) =>
    api.get<RoundDto[]>(`/round/by-competition-season/${competitionSeasonId}`).then(r => r.data),
  get: (id: string) => api.get<RoundDto>(`/round/${id}`).then(r => r.data),
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
  getStandings: (compSeasonId: string) =>
    api.get<StandingDto[]>(`/league/standing/${compSeasonId}`).then(r => r.data),
  getStanding: (compSeasonId: string, teamId: string) =>
    api.get<StandingDto>(`/league/standing/${compSeasonId}/team/${teamId}`).then(r => r.data),
  getScorers: (seasonId: string, topN = 15) =>
    api.get<ScorerDto[]>(`/league/scorer/${seasonId}?topN=${topN}`).then(r => r.data),
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
  // starterIds is the eleven chosen on the lineup screen; the backend validates it
  // again and refuses an illegal one.
  start: (fixtureId: string, userTeamId?: string, starterIds?: string[]) =>
    api.post<MatchCommandResult>(`/match/start/${fixtureId}`, { userTeamId, starterIds })
      .then(r => r.data),
  getLineup: (matchId: string, userTeamId?: string) =>
    api.get<MatchLineupDto>(
      `/match/lineup/${matchId}${userTeamId ? `?userTeamId=${userTeamId}` : ''}`
    ).then(r => r.data),
  getState: (matchId: string) =>
    api.get<MatchStateDto>(`/match/state/${matchId}`).then(r => r.data),
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
  getResult: (matchId: string) =>
    api.get<MatchResult>(`/match/result/${matchId}`).then(r => r.data),
};
