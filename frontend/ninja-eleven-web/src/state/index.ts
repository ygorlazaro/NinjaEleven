import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type {
  TeamDto,
  PlayerDto,
  FixtureDto,
  CompetitionStandingsDto,
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
  standings: CompetitionStandingsDto | null;
  fixtures: FixtureDto[];
  scorers: ScorerDto[];
  leagueSetup: LeagueSetupResult | null;
  feed: FeedEvent[];
  /**
   * The match the feed belongs to.
   *
   * The feed is one list shared by every screen, so without this a manager who played a
   * second match in a row arrived on the first match's log: its events end at a high
   * sequence, the new match starts at 1, and every new event looked already-known and was
   * dropped. The narration did not stop — it was being compared against somebody else's
   * afternoon.
   */
  feedMatchId: string | null;
  lastEvents: MatchEngineEventDto[];
  isPaused: boolean;
  /** The tab on the match screen. 'standings' is the table beside the match. */
  matchScreen: 'stats' | 'lineup' | 'matchday';

  setSelectedTeam: (team: TeamDto | null) => void;
  setLeagueTeams: (teams: TeamDto[]) => void;
  setCurrentMatch: (match: ActiveMatch | null) => void;
  setStandings: (standings: CompetitionStandingsDto | null) => void;
  setFixtures: (fixtures: FixtureDto[]) => void;
  setScorers: (scorers: ScorerDto[]) => void;
  setLeagueSetup: (setup: LeagueSetupResult | null) => void;
  setFeed: (events: FeedEvent[], matchId?: string) => void;
  addFeedEvent: (event: FeedEvent, matchId?: string) => void;
  setLastEvents: (events: MatchEngineEventDto[]) => void;
  setIsPaused: (paused: boolean) => void;
  setMatchScreen: (screen: GameState['matchScreen']) => void;
  setSeasonInfo: (season: SeasonDto | null) => void;
  setCompetitionInfo: (competition: CompetitionDto | null) => void;
  forgetClub: () => void;
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

/**
 * The club the user manages is remembered in this browser, so a reload does not send
 * the user back to the club list while the season is already running. This is where a
 * real session would take over: the stored club would be the one the account owns.
 */
const usePersistedGameState = create<GameState>()(
  persist(
    (set) => ({
      selectedTeam: null,
      selectedSeason: null,
      selectedCompetition: null,
      leagueTeams: [],
      currentMatch: null,
      standings: null,
      fixtures: [],
      scorers: [],
      leagueSetup: null,
      feed: [],
      feedMatchId: null,
      lastEvents: [],
      isPaused: false,
      matchScreen: 'stats',

      setSelectedTeam: (team) => set({ selectedTeam: team }),
      setLeagueTeams: (teams) => set({ leagueTeams: teams }),
      setCurrentMatch: (match) => set({ currentMatch: match }),
      setStandings: (standings) => set({ standings }),
      setFixtures: (fixtures) => set({ fixtures }),
      setScorers: (scorers) => set({ scorers }),
      setLeagueSetup: (setup) => set({ leagueSetup: setup }),

      // A match id given here is the match the log belongs to. Without one the feed is
      // written as belonging to no match at all, which is the honest answer: nobody has
      // said whose log this is yet, so it cannot be merged into anybody's.
      setFeed: (events, matchId) =>
        set({ feed: events, feedMatchId: matchId ?? null }),
      addFeedEvent: (event, matchId) =>
        set(state => {
          if (matchId && state.feedMatchId && state.feedMatchId !== matchId) {
            // Somebody else's log is in here. A single event cannot be appended to it.
            return state;
          }

          return {
            feed: [...state.feed, event],
            feedMatchId: matchId ?? state.feedMatchId,
          };
        }),
      setLastEvents: (events) => set({ lastEvents: events }),
      setIsPaused: (paused) => set({ isPaused: paused }),
      setMatchScreen: (screen) => set({ matchScreen: screen }),
      setSeasonInfo: (season) => set({ selectedSeason: season }),
      setCompetitionInfo: (competition) => set({ selectedCompetition: competition }),
      forgetClub: () =>
        set({ selectedTeam: null, leagueTeams: [], currentMatch: null, feed: [], feedMatchId: null }),
      reset: () =>
        set({
          selectedTeam: null,
          selectedSeason: null,
          selectedCompetition: null,
          leagueTeams: [],
          currentMatch: null,
          standings: null,
          fixtures: [],
          scorers: [],
          leagueSetup: null,
          feed: [],
          feedMatchId: null,
          lastEvents: [],
          isPaused: false,
          matchScreen: 'stats',
        }),
    }),
    {
      name: 'ninja-eleven:career',
      // Only the choice of club is remembered. The feed, the tables and the current
      // match are rebuilt from the backend on every load, because the backend is the
      // one that owns them.
      partialize: state => ({
        selectedTeam: state.selectedTeam,
        selectedSeason: state.selectedSeason,
        selectedCompetition: state.selectedCompetition,
        leagueTeams: state.leagueTeams,
      }),
    }
  )
);

export const useGameState = usePersistedGameState;

/**
 * The log of one match, or nothing.
 *
 * This is the only place that decides whose feed a screen is looking at. Every consumer —
 * the initial load, the hub merge, the recovery fetch and a command's own events — has to
 * agree on that answer, and four copies of the same sequence arithmetic is four chances to
 * compare this match against the last one's.
 */
export const feedOf = (state: GameState, matchId: string): FeedEvent[] =>
  state.feedMatchId === matchId ? state.feed : [];
