import { create } from 'zustand';
import type {
  TeamDto,
  PlayerDto,
  FixtureDto,
  StandingDto,
  ScorerDto,
  MatchStateDto,
  MatchEngineEventDto,
  MatchLineupDto,
  PlayerSeasonStateDto,
  LeagueSetupResult,
  SeasonDto,
  CompetitionDto,
} from '@/types';

interface GameState {
  selectedTeam: TeamDto | null;
  selectedSeason: SeasonDto | null;
  selectedCompetition: CompetitionDto | null;
  leagueTeams: TeamDto[];
  currentMatch: ActiveMatch | null;
  standings: StandingDto[];
  fixtures: FixtureDto[];
  scorers: ScorerDto[];
  leagueSetup: LeagueSetupResult | null;
  feed: FeedEvent[];
  lastEvents: MatchEngineEventDto[];
  isPaused: boolean;
  matchScreen: 'feed' | 'stats' | 'playerStats' | 'tactics' | 'lineup';

  setSelectedTeam: (team: TeamDto | null) => void;
  setLeagueTeams: (teams: TeamDto[]) => void;
  setCurrentMatch: (match: ActiveMatch | null) => void;
  setStandings: (standings: StandingDto[]) => void;
  setFixtures: (fixtures: FixtureDto[]) => void;
  setScorers: (scorers: ScorerDto[]) => void;
  setLeagueSetup: (setup: LeagueSetupResult | null) => void;
  setFeed: (events: FeedEvent[]) => void;
  addFeedEvent: (event: FeedEvent) => void;
  setLastEvents: (events: MatchEngineEventDto[]) => void;
  setIsPaused: (paused: boolean) => void;
  setMatchScreen: (screen: 'feed' | 'stats' | 'playerStats' | 'tactics' | 'lineup') => void;
  setSeasonInfo: (season: SeasonDto | null) => void;
  setCompetitionInfo: (competition: CompetitionDto | null) => void;
  reset: () => void;
}

export interface ActiveMatch {
  matchId: string;
  fixture: FixtureDto;
  lineup: MatchLineupDto;
  state: MatchStateDto;
  playerStats: Record<string, PlayerDto>;
  teamPlayers: {
    home: PlayerSeasonStateDto[];
    away: PlayerSeasonStateDto[];
  };
}

export interface FeedEvent {
  sequence: number;
  minute: number;
  icon: string;
  description: string;
  type: string;
  teamId?: string | null;
  playerId?: string | null;
  homeScore?: number | null;
  awayScore?: number | null;
  onTarget?: boolean;
  isGoal?: boolean;
}

export const useGameState = create<GameState>((set) => ({
  selectedTeam: null,
  selectedSeason: null,
  selectedCompetition: null,
  leagueTeams: [],
  currentMatch: null,
  standings: [],
  fixtures: [],
  scorers: [],
  leagueSetup: null,
  feed: [],
  lastEvents: [],
  isPaused: false,
  matchScreen: 'feed',

  setSelectedTeam: (team) => set({ selectedTeam: team }),
  setLeagueTeams: (teams) => set({ leagueTeams: teams }),
  setCurrentMatch: (match) => set({ currentMatch: match }),
  setStandings: (standings) => set({ standings }),
  setFixtures: (fixtures) => set({ fixtures }),
  setScorers: (scorers) => set({ scorers }),
  setLeagueSetup: (setup) => set({ leagueSetup: setup }),
  setFeed: (events) => set({ feed: events }),
  addFeedEvent: (event) => set((state) => ({
    feed: [...state.feed, event],
  })),
  setLastEvents: (events) => set({ lastEvents: events }),
  setIsPaused: (paused) => set({ isPaused: paused }),
  setMatchScreen: (screen) => set({ matchScreen: screen }),
  setSeasonInfo: (season) => set({ selectedSeason: season }),
  setCompetitionInfo: (competition) => set({ selectedCompetition: competition }),
  reset: () => set({
    selectedTeam: null,
    leagueTeams: [],
    currentMatch: null,
    standings: [],
    fixtures: [],
    scorers: [],
    leagueSetup: null,
    feed: [],
    lastEvents: [],
    isPaused: false,
    matchScreen: 'feed',
  }),
}));
