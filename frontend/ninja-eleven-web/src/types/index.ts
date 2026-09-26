export type Guid = string;

export interface PlayerDto {
  id: Guid;
  name: string;
  birthDate: string;
  age: number;
  position: string;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
}

export interface PlayerSeasonStateDto {
  id: Guid;
  playerId: Guid;
  seasonId: Guid;
  teamId: Guid;
  energy: number;
  goals: number;
  yellowCards: number;
  redCards: number;
  suspensionMatches: number;
  injury: Injury;
  /** Matches of his club the injury still keeps him out of. */
  injuryMatchesRemaining: number;
  isAvailable: boolean;
}

/** Squad entry: static player attributes plus the state in the season. */
export interface SquadPlayerDto {
  id: Guid;
  name: string;
  age: number;
  position: Position;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
  energy: number;
  goals: number;
  yellowCards: number;
  redCards: number;
  suspensionMatches: number;
  injury: Injury;
  /** Matches of his club the injury still keeps him out of. */
  injuryMatchesRemaining: number;
  isAvailable: boolean;
  teamId: Guid;
  seasonId: Guid;
}

export interface TeamDto {
  id: Guid;
  name: string;
  shortName: string;
  primaryColor: string;
  secondaryColor: string;
  rating: number;
}

export interface TeamMembershipDto {
  id: Guid;
  playerId: Guid;
  teamId: Guid;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
}

export interface CompetitionDto {
  id: Guid;
  name: string;
  type: string;
}

export interface SeasonDto {
  id: Guid;
  name: string;
  startDate: string;
  endDate: string;
  status: string;
}

export interface CompetitionSeasonDto {
  id: Guid;
  competitionId: Guid;
  seasonId: Guid;
}

export interface CompetitionParticipantDto {
  id: Guid;
  competitionSeasonId: Guid;
  teamId: Guid;
}

export interface RoundDto {
  id: Guid;
  competitionSeasonId: Guid;
  number: number;
}

export type Injury = 'None' | 'Light' | 'Grave';

export interface FixtureDto {
  id: Guid;
  roundId: Guid;
  homeTeamId: Guid;
  awayTeamId: Guid;
  status: string;
  matchId?: Guid | null;
  homeGoals?: number | null;
  awayGoals?: number | null;
  homeTeam?: TeamDto | null;
  awayTeam?: TeamDto | null;
}

/** Snapshot served by GET /match/{id}: state plus the ordered event log. */
export interface MatchDto {
  id: Guid;
  fixtureId: Guid;
  homeTeamId: Guid;
  awayTeamId: Guid;
  status: string;
  half: MatchHalf;
  currentMinute: number;
  homeScore: number;
  awayScore: number;
  sequence: number;
  seed: number;
  homeTeam?: TeamDto | null;
  awayTeam?: TeamDto | null;
  events: MatchEventDto[];
}

export interface MatchEventDto {
  id: Guid;
  matchId: Guid;
  sequence: number;
  minute: number;
  type: string;
  teamId?: Guid | null;
  playerId?: Guid | null;
  secondaryPlayerId?: Guid | null;
  homeScore: number;
  awayScore: number;
  payload: string;
  description: string;
  icon: string;
}

export type MatchHalf = 'First' | 'Second' | 'ExtraTime' | 'PenaltyShootout';

export interface MatchStateDto {
  matchId: Guid;
  homeScore: number;
  awayScore: number;
  minute: number;
  second: number;
  currentHalf: MatchHalf;
  sequence: number;
  stoppageTimeMinutes: number;
  status: string;
  isPaused: boolean;
  isHalfTime: boolean;
  isFinished: boolean;
  speed: number;
  stats: TeamMatchStatsDto[];
  possession: MatchPossessionDto;
  /**
   * The share of the ball each side has actually had, straight from the engine's own count
   * of the seconds it was in possession. The bar under the scoreboard reads these two and
   * nothing else.
   */
  homePossessionPercent: number;
  awayPossessionPercent: number;
  penaltyAwaitingSelection: boolean;
  /** Who can take the penalty the engine awarded, when one is waiting for the manager. */
  penalty: PenaltyTakerOptionsDto;
  /** The club the manager is watching: what commands are sent for. */
  userTeamId: Guid | null;
  substitutionsUsedHome: number;
  substitutionsUsedAway: number;
  /**
   * The shape each side is playing right now, as three numbers on a team sheet. It moves
   * during the match: a substitution that changes the balance of a side changes the shape
   * it is playing, and that is the point of reading the shape off the eleven.
   */
  formationHome: string;
  formationAway: string;
}

export interface PenaltyTakerOptionsDto {
  awaitingSelection: boolean;
  candidates: MatchPlayerDto[];
}

export interface TeamMatchStatsDto {
  shots: number;
  shotsOnTarget: number;
  corners: number;
  cards: number;
  fouls: number;
  possession: number;
  /** Saves by this side's goalkeeper. */
  saves: number;
}

export interface MatchPossessionDto {
  team: number;
  playerId?: Guid | null;
}

export interface StandingDto {
  id: Guid;
  competitionSeasonId: Guid;
  teamId: Guid;
  team?: TeamDto | null;
  points: number;
  played: number;
  wins: number;
  draws: number;
  losses: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  yellowCards: number;
  redCards: number;
}

export interface ScorerDto {
  playerId: Guid;
  playerName: string;
  age: number;
  goals: number;
  teamId: Guid;
  teamName: string;
}

export interface MatchEngineEventDto {
  sequence: number;
  minute: number;
  type: string;
  teamId?: Guid | null;
  playerId?: Guid | null;
  icon: string;
  description: string;
  homeScore?: number | null;
  awayScore?: number | null;
}

export type MatchCommandResult = {
  accepted: boolean;
  matchId: Guid;
  errorMessage?: string | null;
  events?: MatchEngineEventDto[] | null;
};

/**
 * The beats of one match of the round, sent to everybody following the round.
 *
 * The match id is what keeps the four logs apart: a client watching its own match in full
 * receives this for the other three at the same time, and the words are the same ones
 * those matches tell their own followers.
 */
export interface MatchdayEventDto {
  roundId: Guid;
  matchId: Guid;
  events: MatchEngineEventDto[];
}

/**
 * One shape a manager can order the eleven to be built in. The numbers are the three
 * outfield bands; the screen reads the name, and sends the code back on kick-off.
 */
export type TacticDto = {
  code: string;
  name: string;
  defenders: number;
  midfielders: number;
  attackers: number;
};

/**
 * A played round told back. The summary is the goals' own narration, read out of the
 * events the match recorded — never a sentence the screen wrote.
 */
export type MatchdayReportDto = {
  roundId: Guid;
  roundNumber: number;
  entries: MatchdayReportEntryDto[];
};

export type MatchdayReportEntryDto = {
  fixtureId: Guid;
  matchId: Guid;
  homeTeamId: Guid;
  homeTeamName: string;
  homeShortName: string;
  awayTeamId: Guid;
  awayTeamName: string;
  awayShortName: string;
  homeGoals: number;
  awayGoals: number;
  summary: string;
};

export interface LineupDto {
  starters: PlayerDto[];
  bench: PlayerDto[];
  formation: string;
}

export interface MatchLineupDto {
  matchId: Guid;
  userTeamIndex: number;
  homeTeam: TeamDto;
  awayTeam: TeamDto;
  homeLineup: MatchPlayerDto[];
  awayLineup: MatchPlayerDto[];
  homeBench: MatchPlayerDto[];
  awayBench: MatchPlayerDto[];
}

export interface MatchPlayerDto {
  playerId: Guid;
  name: string;
  age: number;
  position: string;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  reflexes: number;
  goalkeeperPower: number;
  energy: number;
  seasonYellowCards: number;
  suspensionMatches: number;
  matchYellowCards: number;
  redCard: boolean;
  emergencyGK: boolean;
  injuredOff: boolean;
  subbedIn: boolean;
  /**
   * He left the pitch and is spent. A substitute who has been taken off cannot be named
   * again, so the substitution screen stops offering him.
   */
  subbedOff: boolean;
  /** Goals for the season, and so the number on a player profile. */
  goals: number;
  /**
   * Goals in this match, and so the number on the eleven card under the scoreboard. A
   * striker with nine for the season who has not scored today has scored nothing here.
   */
  matchGoals: number;
  /**
   * Own goals in this match, and the red ball on his card. Nobody's favourite statistic and
   * it belongs to him all the same.
   */
  matchOwnGoals: number;
  /** Saves this goalkeeper made in this match. */
  matchSaves: number;
  matchStats?: PlayerMatchStatsDto | null;
  /** Chance of converting a penalty right now, only sent for the candidates of a penalty. */
  penaltyChance?: number | null;
}

export interface PlayerMatchStatsDto {
  fouls: number;
  cards: number;
  corners: number;
  saves: number;
  shots: number;
  shotsOnTarget: number;
  goals: number;
  injuries: number;
  cardMinutes: number[];
  goalMinutes: number[];
  saveMinutes: number[];
  injuryMinutes: number[];
}

export interface MatchResult {
  homeScore: number;
  awayScore: number;
  playerStats: PlayerMatchStatsDto[];
  homeShots: number;
  awayShots: number;
  homeShotsOnTarget: number;
  awayShotsOnTarget: number;
  homeCorners: number;
  awayCorners: number;
  homeCards: number;
  awayCards: number;
  homeFouls: number;
  awayFouls: number;
  homePossession: number;
  awayPossession: number;
  formationHome: string;
  formationAway: string;
  substitutionsHome: number;
  substitutionsAway: number;
}

export interface LeagueSetupResult {
  competitionSeasonId: Guid;
  competitionId: Guid;
  seasonId: Guid;
  rounds: RoundDto[];
  fixtures: FixtureDto[];
}

export interface StandingTableRow {
  pos: number;
  team: TeamInfo;
  pts: number;
  played: number;
  w: number;
  d: number;
  l: number;
  gf: number;
  ga: number;
  gd: number;
  yellow: number;
  red: number;
}

export interface TeamInfo {
  id: Guid;
  name: string;
  shortName: string;
  primaryColor: string;
  secondaryColor: string;
  rating: number;
}

export interface PlayerInfo {
  id: Guid;
  name: string;
  age: number;
  position: string;
  energy: number;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
  seasonGoals?: number;
  seasonYellowCards?: number;
  redCard?: boolean;
  suspensionRounds?: number;
  suspensionMatches?: number;
  injury?: string | null;
  injuryRoundsRemaining?: number;
  emergencyGK?: boolean;
  matchYellowCards?: number;
  matchStats?: {
    shots: number;
    shotsOnTarget: number;
    goals: number;
    cards: number;
    fouls: number;
    saves: number;
    corners: number;
  };
}

export type Position = 'GK' | 'DEF' | 'MID' | 'ATT';

export type CardType = 'yellow' | 'red';

/** Fixtures of a round that were played without a manager watching. */
export interface RoundSimulationResult {
  roundId: string;
  playedMatchIds: string[];
}

/**
 * A player whole: who he is, the season's line and the career's, and every match he has
 * a line in. The history comes back whole and the screen filters it, so the season filter
 * and the totals can never be two different sets of matches.
 */
export type PlayerProfileDto = {
  playerId: Guid;
  name: string;
  position: Position;
  age: number;
  birthDate: string;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
  seasonId?: Guid | null;
  teamId?: Guid | null;
  teamName: string;
  energy: number;
  isAvailable: boolean;
  injury: string;
  injuryMatchesRemaining: number;
  season: PlayerCareerLineDto;
  total: PlayerCareerLineDto;
  history: PlayerMatchLineDto[];
};

/**
 * Appearances is a pair, not a number: "14 (3)" is fourteen matches and three of them off
 * the bench, and a single figure cannot say which.
 */
export type PlayerCareerLineDto = {
  appearances: number;
  started: number;
  cameOn: number;
  goals: number;
  ownGoals: number;
  saves: number;
  yellowCards: number;
  redCards: number;
  injuries: number;
  matchesMissed: number;
};

export type PlayerMatchLineDto = {
  matchId: Guid;
  seasonId?: Guid | null;
  started: boolean;
  cameOn: boolean;
  subbedOff: boolean;
  goals: number;
  ownGoals: number;
  saves: number;
  yellowCards: number;
  redCards: number;
  wasInjured: boolean;
  injuredOff: boolean;
  isHome: boolean;
  opponentName: string;
  homeGoals: number;
  awayGoals: number;
  roundNumber: number;
};
