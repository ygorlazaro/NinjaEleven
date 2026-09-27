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
  stars: number;
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
  stars: number;
  energy: number;
  goals: number;
  /** Saves for the club this season. Zero for everyone who is not a goalkeeper. */
  saves: number;
  yellowCards: number;
  redCards: number;
  suspensionMatches: number;
  injury: Injury;
  /** Matches of his club the injury still keeps him out of. */
  injuryMatchesRemaining: number;
  /** How many times he has been hurt this season, which the market reads. */
  injuries: number;

  /**
   * The money, in limos: what he is worth, what another club would have to pay for him and
   * what the club owes for him this season. The first and the second are different numbers
   * on purpose — a player with years left on his contract costs a fifth more than he is
   * worth, and a table that showed only the first would have a manager offering the second.
   */
  marketValue: number;
  askingPrice: number;
  salary: number;
  /** Seasons of the contract, and how many of them are left. */
  contractSeasons: number;
  seasonsLeft: number;
  isInLastSeason: boolean;

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
  stars: number;
  stadium?: StadiumDto | null;
}

export interface StadiumDto {
  id: Guid;
  /** A ground has a name of its own, not one composed from the club's name. */
  name: string;
  capacity: number;
  ticketPrice: number;
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

/**
 * One edition of a competition inside one season.
 *
 * This is what a club list, a table and a fixture list are all addressed by. "Campeonato
 * Brasileiro" runs three times in a season, once per tier, and a list of competitions has
 * nowhere to say which of the three a club is in.
 */
export interface CompetitionEditionDto {
  /** The edition's id: what a table, a fixture and a trophy are addressed by. */
  id: string;
  competitionId: string;
  seasonId: string;
  divisionId?: string | null;
  /** 1 is the top of the pyramid. Null for a cup and a Supercup. */
  tier?: number | null;
  /** The competition's own name, without the tier. */
  competitionName: string;
  type: CompetitionType;
  /** "1ª Divisão" for a division, or the competition's name for a knockout. */
  name: string;
  /** Whether this edition is one tier's table rather than a knockout. */
  isDivision: boolean;
}

export type CompetitionType = 'League' | 'Cup' | 'SuperCup';

/**
 * The divisions of a season, top first.
 *
 * It is a derived list rather than a stored one: a division is a tier of the pyramid, and
 * the tiers are a rule (`CompetitionRules.DivisionCount`) rather than a fact a database has
 * to be told. Reading them off the editions the season actually has keeps the two in step —
 * a pyramid with a division in it and a list of divisions that disagrees is a manager who
 * picks a 4ª Divisão that nobody plays in.
 */
export const divisionsOf = (editions: CompetitionEditionDto[]) =>
  editions
    .filter(edition => edition.isDivision && edition.tier != null)
    .sort((a, b) => (a.tier! - b.tier!));

export interface SeasonDto {
  id: Guid;
  /** The season's number, counted from one. Its identity, and what orders the list. */
  number: number;
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
  /** Counted from one inside its own competition, which is why it is not a date. */
  number: number;
  /** The matchday this window belongs to: the link between a cup tie and a league game. */
  matchDayId?: Guid | null;
  /** Which window of the matchday: the championship is 1, the cup is 2. */
  window: number;
  completedAt?: string | null;
}

/** One day of a season's football, with a date. */
export interface MatchDayDto {
  id: Guid;
  seasonId: Guid;
  number: number;
  date: string;
}

/**
 * A season's calendar: the days, and the windows of football scheduled on them.
 *
 * It is asked for as a whole because a calendar that showed a fixture without the day it is
 * on is a list, and a manager reading a season wants to know when his club plays rather than
 * only in which order.
 */
export interface SeasonCalendarDto {
  seasonId: Guid;
  seasonName: string;
  matchDayCount: number;
  matchDays: MatchDayDto[];
  windows: RoundDto[];
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
  attendance: number;
  gateRevenue: number;
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
  /**
   * A man who cannot carry on and whose replacement the manager has to name. It is not a
   * notification about something that has happened: the clock is held while it is out, so
   * while this is set the match is waiting on this and nothing else is moving.
   */
  injury: MatchInjuryDto;
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
  /** Attendance at kick-off. */
  attendance: number;
  /** Gate revenue in limos. */
  gateRevenue: number;
}

export interface PenaltyTakerOptionsDto {
  awaitingSelection: boolean;
  candidates: MatchPlayerDto[];
}

/**
 * Who is hurt, for which club, and how bad it is. He is still in the eleven at this point,
 * because the change that takes him off is the change the manager has not made yet.
 */
export interface MatchInjuryDto {
  awaitingSubstitution: boolean;
  playerId: Guid | null;
  playerName: string | null;
  /** Which side he plays for: 1 home, 2 away. */
  team: number | null;
  /** None whenever there is nothing to report, so a healthy match does not read as hurt. */
  severity: Injury;
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

/** The band a club sits in across a divisions table, worked out by the backend. */
export type TableZone = 'None' | 'Safe' | 'Promotion' | 'Relegation';

/**
 * One line of a classification table, as the backend worked it out.
 *
 * `position` is decided by the backend and is not recomputed here. A table sorted again in
 * the browser is a table that can disagree with the promotion rules, and a manager told his
 * club is sixth by the promotion pass and seventh by the screen has been given two answers to
 * one question.
 */
export interface StandingDto {
  teamId: Guid;
  team?: TeamDto | null;
  position: number;
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
  stars: number;
  /** Which band of the table this line is in, from the backend's own rules. */
  zone?: TableZone;
}

/**
 * A table as a manager reads it: where the clubs are, and where they would be if the games
 * still being played went a certain way.
 *
 * Both tables are ordered by the same rules and the same tiebreakers, so a club that is sixth
 * officially can be third live without either screen disagreeing with itself. `projected`
 * counts every unfinished game of the competition at its current score, not only the match on
 * screen: a projection that ignored the other games of the matchday would be wrong in a way
 * nobody could see, since noticing it means watching a match the manager is not in.
 */
export interface CompetitionStandingsDto {
  competitionSeasonId: Guid;
  seasonId?: Guid | null;
  divisionId?: Guid | null;
  /** 1 is the top of the pyramid. Null for a cup and a Supercup. */
  tier?: number | null;
  competitionName: string;
  official: StandingDto[];
  projected: StandingDto[];
  hasLiveMatches: boolean;
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
  /**
   * How bad the knock of this match was. It is a separate fact from `injuredOff`, because
   * a player carrying a light injury is still on the pitch: he is hurt and still playing,
   * and a screen that reads `injuredOff` alone shows a healthy eleven.
   */
  injury: Injury;
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
  stars: number;
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
  stars: number;
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
  stars: number;
  seasonId?: Guid | null;
  teamId?: Guid | null;
  teamName: string;
  energy: number;
  isAvailable: boolean;
  injury: string;
  injuryMatchesRemaining: number;

  /**
   * The money, in limos, read from the same contract the squad table reads: what he is
   * worth, what a rival would have to pay to take him, and what this club owes him for the
   * season. `seasonsLeft` of `contractSeasons` is the clock on the deal, and it is what makes
   * the price a price rather than a valuation.
   */
  marketValue: number;
  askingPrice: number;
  salary: number;
  contractSeasons: number;
  seasonsLeft: number;
  isInLastSeason: boolean;

  season: PlayerCareerLineDto;
  total: PlayerCareerLineDto;
  history: PlayerMatchLineDto[];
  /**
   * The player's face, as the raw JSON of a faces.js FaceConfig, and null when he has none.
   * It stays a string on this side of the wire on purpose — the shape belongs to the library
   * that draws it, and a copy of it in TypeScript would be a second one to keep in step.
   * `parseFace` is the only thing that reads it.
   */
  face?: string | null;
};

/**
 * Appearances is a pair, not a number: "14 (3) [2]" is fourteen matches, three of them off
 * the bench, and two on the bench unused. A single figure cannot say which.
 */
export type PlayerCareerLineDto = {
  appearances: number;
  started: number;
  cameOn: number;
  benchUnused: number;
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
  wasOnBenchUnused: boolean;
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

export type SquadSuggestionDto = {
  starterIds: Guid[];
  benchIds: Guid[];
};

/**
 * One finished match of a club, from the club's side. `goalsFor` and `goalsAgainst` are
 * already ordered for the club, so a screen does not have to know which end it was on.
 */
export interface TeamMatchRecordDto {
  matchId: Guid;
  opponentName: string;
  /** So the opponent's name is a door to that club, as every name in the game is. */
  opponentTeamId: Guid;
  isHome: boolean;
  goalsFor: number;
  goalsAgainst: number;
  roundNumber: number;
  playedAt: string;
}

/**
 * One line of a club's books.
 *
 * `amount` is signed — positive money in, negative money out — and `balanceAfter` is the
 * balance the club was left with after the line was booked. The running balance is the
 * backend's to say and not the screen's to work out: a page of a ledger that starts halfway
 * down the history has no way of knowing what came before it, so a screen that added the
 * column up itself would be right on the first page and wrong on the second.
 *
 * `statesABalance` says the line is not a movement at all but a statement of what the club
 * had — the capital it was founded on, the balance a season was handed. Such a line is drawn
 * as a balance, because calling it money in would show the manager a fortune twice.
 */
export interface FinanceMovementDto {
  id: Guid;
  /** The season the money moved in. A career outlives a season and the books are read per season. */
  seasonId: Guid;
  seasonNumber: number;
  seasonName: string;
  /** The day of the season the line belongs to, or null for one that belongs to no day. */
  matchDayNumber: number | null;
  /** A stable key, as a name. The mark and the words beside it are the screen's. */
  kind: string;
  description: string;
  amount: number;
  balanceAfter: number;
  statesABalance: boolean;
}

/**
 * A page of a club's book and the three numbers above it.
 *
 * The page and the summary arrive together because they are asked about together, and a
 * screen that fetched the totals separately could draw a balance and an income from two
 * different moments of a matchday still being played. The totals are of the filter asked for:
 * with no season chosen they are of the whole career, and with one chosen they are of that
 * season. The balance is always the club's own, because the club has one balance whichever
 * season is being read.
 */
export interface FinanceLedgerDto {
  balance: number;
  income: number;
  expenses: number;
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  movements: FinanceMovementDto[];
}

/**
 * What kind of thing happened to a club.
 *
 * The key is the contract and the sentence beside it is the club's own account of the event,
 * the same way the match feed's event types are a contract and the narration is the screen's.
 * A kind the screen has never seen still gets a line and a mark, because refusing to show a
 * moment of a club's history is not the same as not understanding it.
 */
export type ClubHistoryKind =
  | 'FirstSeason'
  | 'NameChange'
  | 'CrestChange'
  | 'StadiumUpgrade'
  | 'TopScorer'
  | 'Title'
  | 'Promotion'
  | 'Relegation';

/** One moment in a club's history, as the shelf of memory says it. */
export type ClubHistoryEventDto = {
  id: string;
  kind: ClubHistoryKind | string;
  seasonNumber: number;
  seasonName: string;
  /** The club's account of what happened, in a sentence. */
  description: string;
  /** What the moment carried, when it carried a number: goals in a season, a season won. */
  value?: number | null;
};

/**
 * A place on a podium, which is a different claim from the place next to it.
 *
 * "Champion of the 1st division" and "champion of the 3rd" are not the same trophy, and the
 * division is carried with it so a shelf can say which one it is holding rather than a number
 * of medals with nothing to tell them apart.
 */
export type ClubTrophyDto = {
  id: string;
  /** The competition as the shelf names it. */
  competition: string;
  kind: 'Champion' | 'RunnerUp' | 'Third';
  seasonNumber: number;
  seasonName: string;
  divisionName?: string | null;
};

/**
 * A club, whole: who it is, who runs it, what it costs to keep, what it has won, where it
 * has been and what has happened to it.
 *
 * The fields are the ones the screen shows and the numbers are the ones a club is judged by,
 * so a screen that assembled its own totals would be answering a question the backend already
 * has. `squadSize` and `balance` are read from the roster and the book rather than counted
 * here: a club's strength is the sum of the two the game already keeps.
 */
export type ClubProfileDto = {
  teamId: Guid;
  name: string;
  shortName: string;
  primaryColor: string;
  secondaryColor: string;
  /** The manager's name. A club has a manager before it has a stadium. */
  coachName: string;
  /** How many men are on the books, which is not the eleven. */
  squadSize: number;
  /** What the club has in the bank, in limos. */
  balance: number;
  /** Times the club has gone up a division and times it has come down one. */
  promotions: number;
  relegations: number;
  /** Everything on the shelf, newest first. */
  trophies: ClubTrophyDto[];
  /** The club's history, newest first. */
  history: ClubHistoryEventDto[];
};

/**
 * A ground, as a manager sees it: where it is, how big it is and what a seat costs.
 *
 * The two prices are two numbers on purpose. A league match and a cup tie are not the same
 * occasion — a cup tie is a match somebody will travel for, and a season ticket is worth
 * nothing on a night when the opposition is a second-division club — and a ground that sells
 * both at the championship price prices the league and the cup at the same thing. The
 * `league` and `cup` names are the screen's; the division factor the attendance model
 * applies to a price is the engine's and is not restated here.
 */
export type StadiumProfileDto = {
  teamId: Guid;
  name: string;
  city: string;
  capacity: number;
  /** What a seat costs for a match of the championship. */
  leagueTicketPrice: number;
  /** What a seat costs for a match of the cup. */
  cupTicketPrice: number;
  /** The club's colours, so the ground can be drawn in them. */
  primaryColor: string;
  secondaryColor: string;
};

/**
 * A sponsor's offer, which is money a club does not have to earn on a Saturday.
 *
 * A sponsorship is paid per match rather than per season because that is how one is sold: a
 * club takes a sponsor's money against the games it plays, and a sponsor whose name is on a
 * shirt for a season that never happens has been promised a season that does not exist. The
 * count is the number of matches the club has left to play under the deal, which is what
 * decides when a club may change its mind about it.
 */
export type SponsorOfferDto = {
  id: string;
  name: string;
  /** The sector the money comes from, which is what a shirt says above the name. */
  industry: string;
  /** What the sponsor pays for a match, in limos. */
  perMatchFee: number;
  /** The length of the deal in matches, which is the contract the club signs. */
  contractMatches: number;
  /** The mark's own colour, so a sponsor is a thing the screen can draw. */
  color: string;
};

/**
 * A club's sponsor book: who is on the shirt, and who is waiting to be.
 *
 * `matchesLeft` is the whole of the rule that governs a change: a club that has matches left
 * on its deal has already been paid for them, and the sponsor's name is what was sold. So the
 * deal has to run out before another one can start, and a screen that let a manager change it
 * on any matchday would be a club taking money for games it is still going to play under
 * somebody else's name.
 */
export type SponsorBookDto = {
  teamId: Guid;
  /** The sponsor on the shirt, and the deal it was signed on. */
  current: SponsorOfferDto;
  /** Matches of the deal still to be played. Zero is the only moment a change is allowed. */
  matchesLeft: number;
  /** The offers on the table, five of them, which is a shortlist and not a market. */
  candidates: SponsorOfferDto[];
  /** The one the club is carrying, so the card can say who it is at a glance. */
  masterSponsorId: string;
};

/**
 * One line of a club's scorers table: a man of that club, his goals in a season, and whether
 * he is still there.
 *
 * The goals are the sum of his match lines and the appearances are the two counts a manager
 * reads — games started, and games entered off the bench — because "14 (3)" is a fact about
 * how the staff trusted a man, and a single number throws that away.
 *
 * `isStillAtClub` is the field that keeps the table honest. A scorer who has left is still on
 * the list with the flag against him: a club's all-time scorers is the one page that must
 * never lose a name, and a screen that showed only the men under contract would quietly
 * rewrite the club's history every time a window opened. The flag is decided by the backend,
 * which is the only thing in the game that knows a contract from a shirt.
 */
export type ClubScorerDto = {
  playerId: Guid;
  playerName: string;
  age: number;
  position: number;
  goals: number;
  ownGoals: number;
  started: number;
  cameOn: number;
  /** Goals per appearance, decided by the backend; null when he never appeared. */
  goalsPerAppearance: number | null;
  isStillAtClub: boolean;
};

/**
 * The kinds of competition a club's goals can be counted over.
 *
 * These are the world's own names and they are the query's, not the screen's: the request
 * carries one of them and the backend binds it to the enum, so a client asking for a cup and
 * being handed league goals would be given a number that answers a different question, with
 * nothing in the answer to say so.
 */
export type CompetitionFilter = 'League' | 'Cup' | 'SuperCup';

export const COMPETITION_LABELS: Record<CompetitionFilter, string> = {
  League: 'Liga',
  Cup: 'Copa',
  SuperCup: 'Supercopa'
};
