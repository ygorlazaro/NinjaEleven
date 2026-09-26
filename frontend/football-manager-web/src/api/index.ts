import api from './client';
import type {
  PlayerDto, CompetitionDto, SeasonDto, TeamDto,
  FixtureDto, StandingDto, ScorerDto, LeagueSetupResult,
  MatchLineupDto, MatchStateDto, MatchCommandResult, MatchEngineEventDto,
  MatchResult, PlayerSeasonStateDto
} from '../types';

export const PlayerApi = {
  list: () => api.get<PlayerDto[]>('/player').then(r => r.data),
  get: (id: string) => api.get<PlayerDto>(`/player/${id}`).then(r => r.data),
  create: (data: any) => api.post<PlayerDto>('/player', data).then(r => r.data),
  update: (id: string, data: any) => api.put(`/player/${id}`, data).then(r => r.data),
  delete: (id: string) => api.delete(`/player/${id}`),
  getSeasonState: (playerId: string, seasonId: string) =>
    api.get<PlayerSeasonStateDto>(`/player/${playerId}/season/${seasonId}`).then(r => r.data),
};

export const TeamApi = {
  list: () => api.get<TeamDto[]>('/team').then(r => r.data),
  get: (id: string) => api.get<TeamDto>(`/team/${id}`).then(r => r.data),
  create: (data: any) => api.post<TeamDto>('/team', data).then(r => r.data),
  update: (id: string, data: any) => api.put(`/team/${id}`, data).then(r => r.data),
  delete: (id: string) => api.delete(`/team/${id}`),
  getSquad: (teamId: string, seasonId: string) =>
    api.get<PlayerSeasonStateDto[]>(`/team/${teamId}/squad/${seasonId}`).then(r => r.data),
};

export const CompetitionApi = {
  list: (seasonId: string) =>
    api.get<CompetitionDto[]>(`/competitions/by-season/${seasonId}`).then(r => r.data),
  get: (id: string) => api.get<CompetitionDto>(`/competitions/${id}`).then(r => r.data),
  create: (data: any) => api.post<CompetitionDto>('/competitions', data).then(r => r.data),
};

export const SeasonApi = {
  list: () => api.get<SeasonDto[]>('/seasons').then(r => r.data),
  get: (id: string) => api.get<SeasonDto>(`/seasons/${id}`).then(r => r.data),
  create: (data: any) => api.post<SeasonDto>('/seasons', data).then(r => r.data),
};

export const LeagueApi = {
  setup: (competitionId: string, seasonId: string, teamIds: string[]) =>
    api.post<LeagueSetupResult>('/league/setup', { competitionId, seasonId, teamIds })
      .then(r => r.data),
  getFixture: (fixtureId: string) =>
    api.get<FixtureDto>(`/league/fixture/${fixtureId}`).then(r => r.data),
  getFixturesForRound: (compSeasonId: string, round: number) =>
    api.get<FixtureDto[]>(`/league/fixtures/round/${compSeasonId}/${round}`)
      .then(r => r.data),
  getStandings: (compSeasonId: string) =>
    api.get<StandingDto[]>(`/league/standings/${compSeasonId}`).then(r => r.data),
  getStanding: (compSeasonId: string, teamId: string) =>
    api.get<StandingDto>(`/league/standing/${compSeasonId}/${teamId}`).then(r => r.data),
  getScorers: (seasonId: string, topN = 15) =>
    api.get<ScorerDto[]>(`/league/scorers/${seasonId}?topN=${topN}`).then(r => r.data),
};

export const MatchApi = {
  start: (fixtureId: string) =>
    api.post<MatchCommandResult>(`/matches/start/${fixtureId}`).then(r => r.data),
  getLineup: (matchId: string) =>
    api.get<MatchLineupDto>(`/matches/match-lineup/${matchId}`).then(r => r.data),
  getState: (matchId: string) =>
    api.get<MatchStateDto>(`/matches/state/${matchId}`).then(r => r.data),
  selectLineup: (matchId: string, teamId: string, starterIds: string[]) =>
    api.post<MatchCommandResult>(`/matches/select-lineup/${matchId}/${teamId}`, { starterIds })
      .then(r => r.data),
  substitute: (matchId: string, teamId: string, playerOut: string, playerIn: string) =>
    api.post<MatchCommandResult>(`/matches/substitute/${matchId}/${teamId}`, { playerOutId: playerOut, playerInId: playerIn })
      .then(r => r.data),
  selectPenaltyTaker: (matchId: string, teamId: string, playerId: string) =>
    api.post<MatchCommandResult>(`/matches/penalty-taker/${matchId}/${teamId}`, { playerId })
      .then(r => r.data),
  pause: (matchId: string) =>
    api.post<MatchCommandResult>(`/matches/pause/${matchId}`).then(r => r.data),
  resume: (matchId: string) =>
    api.post<MatchCommandResult>(`/matches/resume/${matchId}`).then(r => r.data),
  changeSpeed: (matchId: string, speed: number) =>
    api.post<MatchCommandResult>(`/matches/speed/${matchId}/${speed}`).then(r => r.data),
  continueSecondHalf: (matchId: string) =>
    api.post<MatchCommandResult>(`/matches/continue-second-half/${matchId}`).then(r => r.data),
  tick: (matchId: string) =>
    api.post<MatchEngineEventDto[]>(`/matches/tick/${matchId}`).then(r => r.data),
  getResult: (matchId: string) =>
    api.get<MatchResult>(`/matches/match-result/${matchId}`).then(r => r.data),
};
