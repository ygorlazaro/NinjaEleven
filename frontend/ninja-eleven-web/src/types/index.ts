export type Guid = string;

export interface PlayerDto {
  id: Guid;
  name: string;
  age: number;
  position: string;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
  /**
   * The ceiling on his reading: the best player of this position the world expects him
   * to become. It arrives from the backend and is never worked out here.
   */
  potential: number;
  /**
   * How much football is left in him at minute eighty-five, on the same 1..100 scale as the
   * attributes. The backend sends it on the squad and the player rows alike; it is here
   * because the contract carries it, and the training panel reads it as the reason two men
   * with the same numbers pay different prices for the same session.
   */
  stamina: number;
  stars: number;
}

/**
 * One training session's result. A session spends energy and gets a point, and both numbers
 * come back so a screen can say what it cost without refetching the whole squad.
 */
export interface TrainingResultDto {
  playerId: Guid;
  seasonId: Guid;
  attribute: string;
  energySpent: number;
  energyLeft: number;
  attributeBefore: number;
  attributeAfter: number;
  /** What the session cost the club, being a share of the man's season wage. */
  fee: number;
  /** How many sessions the club has left on the day after this one. */
  sessionsLeft: number;
}

/**
 * One of the eight attributes, as the wire names it. It is the same closed set the backend
 * holds, and it is spelled out here rather than left as a string so that a cell is a
 * `PlayerAttribute` and not a name that happens to match.
 */
export type PlayerAttribute =
  | 'Speed'
  | 'Accuracy'
  | 'Dribbling'
  | 'Heading'
  | 'Strength'
  | 'GoalkeeperPower'
  | 'Reflexes'
  | 'Stamina';

/**
 * One attribute on a training sheet. The cost is null when a session is impossible — an
 * attribute that is not this man's, or one already at his ceiling — which is what lets a
 * screen offer nothing instead of offering a button the backend will refuse. The price is
 * arrived at by the domain and is never worked out here.
 */
export interface TrainingAttributeQuoteDto {
  attribute: PlayerAttribute;
  value: number;
  cost: number | null;
}

/** One player's training sheet: who he is, what he has, and the price of each of the eight. */
export interface TrainingQuoteDto {
  playerId: Guid;
  name: string;
  position: Position;
  age: number;
  potential: number;
  stamina: number;
  energy: number;
  isAvailable: boolean;
  injury: string;
  /**
   * What one session on this man costs the club, being a share of his season wage. It is a
   * number the domain arrived at and is never worked out here — the same session costs the
   * club far more of a striker's wage than of a reserve goalkeeper's, so it is per man and
   * not a single figure for the squad.
   */
  sessionFee: number;
  attributes: TrainingAttributeQuoteDto[];
}

/** The club's whole sheet, with what the squad has left between them. */
export interface SquadTrainingQuotesDto {
  teamId: Guid;
  seasonId: Guid;
  squadEnergy: number;
  /** The calendar day the allowance below is for, sent so the screen can name it. */
  day: string;
  /** Whether the club has a fixture that day, which is what makes it one session or two. */
  playsToday: boolean;
  /** How many sessions the club has that day. */
  sessionsAllowed: number;
  /** How many of them have been spent. */
  sessionsSpent: number;
  players: TrainingQuoteDto[];
}

export interface PlayerSeasonStateDto {
  id: Guid;
  playerId: Guid;
  seasonId: Guid;
  /** Null for a free agent. */
  teamId?: Guid | null;
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
  /**
   * The ceiling on his reading: the best player of this position the world expects him
   * to become. It arrives from the backend and is never worked out here.
   */
  potential: number;
  /**
   * How much football is left in him at minute eighty-five, on the same 1..100 scale as the
   * attributes. The backend sends it on the squad and the player rows alike; it is here
   * because the contract carries it, and the training panel reads it as the reason two men
   * with the same numbers pay different prices for the same session.
   */
  stamina: number;
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
  /** Whether the player has declared he will retire at the end of the season. */
  retiring: boolean;
  /** Null for a free agent: a player with no club is available to anyone. */
  teamId?: Guid | null;
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
  /** True if the club has a human manager, false if NPC-controlled, null if no manager row exists. */
  controlledBy?: boolean | null;
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

export type TransferStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Completed' | 'Expired';

export interface TransferListingDto {
  playerId: Guid;
  name: string;
  position: string;
  age: number;
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
  /**
   * The ceiling on his reading: the best player of this position the world expects him
   * to become. It arrives from the backend and is never worked out here.
   */
  potential: number;
  /**
   * How much football is left in him at minute eighty-five, on the same 1..100 scale as the
   * attributes. The backend sends it on the squad and the player rows alike; it is here
   * because the contract carries it, and the training panel reads it as the reason two men
   * with the same numbers pay different prices for the same session.
   */
  stamina: number;
  stars: number;
  teamId?: Guid | null;
  teamName?: string | null;
  teamPrimaryColor?: string | null;
  teamSecondaryColor?: string | null;
  /**
   * Whether he is a free agent: nobody to buy him from, so a club signs him for a signing fee
   * rather than a price.
   */
  isFreeAgent: boolean;
  /**
   * Whether somebody already has a live deal on him — a proposal waiting for an answer or one
   * already agreed, the manager's own included. The market refuses a second offer on a man who
   * is spoken for, so the row says it before the button does.
   */
  hasActiveProposal: boolean;
  energy: number;
  injury: string;
  injuryMatchesRemaining: number;
  retiring: boolean;
  marketValue?: number | null;
  salary?: number | null;
  contractSeasons: number;
  seasonsLeft: number;
  isInLastSeason: boolean;
  askingPrice?: number | null;
  season: PlayerCareerLineDto;
  total: PlayerCareerLineDto;
  /** The career split by club, filled in on the card that opens from a row. */
  clubs: PlayerClubCareerLineDto[];
}

/** One club's share of a player's career, and the seasons he spent in that shirt. */
export interface PlayerClubCareerLineDto {
  teamId: Guid;
  teamName: string;
  seasons: number;
  total: PlayerCareerLineDto;
}

export interface TransferProposalDto {
  transferId: Guid;
  playerId: Guid;
  playerName: string;
  playerPosition: string;
  playerAge: number;
  /** Null when the player has no club: a signing, not a purchase. */
  sellingClubId?: Guid | null;
  sellingClubName: string;
  buyingClubId: Guid;
  buyingClubName: string;
  proposalSeasonNumber: number;
  arrivalSeasonNumber: number;
  /** The round he walks in on: the eleventh of this season, or the first of the next. */
  arrivalRoundNumber?: number | null;
  fee: number;
  status: TransferStatus;
  proposedAt: string;
  resolvedAt?: string | null;
  completedAt?: string | null;
}

export interface TransferInboxDto {
  clubId: Guid;
  clubName: string;
  proposalSeasonNumber: number;
  incoming: TransferProposalDto[];
  outgoing: TransferProposalDto[];
}

/**
 * Where the world is in its transfer calendar, told by the backend rather than worked out here:
 * a number a manager reads is a number the rules decided, and a screen that counted the rounds
 * itself would eventually count them differently from the calendar they are played on.
 */
export interface TransferWindowStateDto {
  seasonNumber: number;
  currentRound: number;
  isOpen: boolean;
  arrivalSeasonNumber: number;
  arrivalRoundNumber: number;
  arrivalLabel: string;
}

export interface TransferSearchResultDto {
  players: TransferListingDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  window: TransferWindowStateDto;
}

/** Everything the market is narrowed by. An absent filter narrows nothing. */
export interface TransferSearchFilters {
  position?: 'GK' | 'DEF' | 'MID' | 'ATT';
  minAge?: number;
  maxAge?: number;
  minStars?: number;
  maxStars?: number;
  minSpeed?: number;
  minAccuracy?: number;
  minDribbling?: number;
  minHeading?: number;
  minStrength?: number;
  minGoalkeeperPower?: number;
  minReflexes?: number;
  retiring?: boolean;
  freeAgentsOnly?: boolean;
  withClubOnly?: boolean;
  teamId?: string;
}

export interface TransferHistoryLineDto {
  playerId: Guid;
  playerName: string;
  playerPosition: string;
  /** Null when nobody sold him: a free agent who was signed has a history line too. */
  sellingClubId?: Guid | null;
  sellingClubName: string;
  buyingClubId: Guid;
  buyingClubName: string;
  fee: number;
  status: TransferStatus;
  proposedAt: string;
  resolvedAt?: string | null;
  completedAt?: string | null;
  proposalSeasonNumber: number;
  arrivalSeasonNumber: number;
  arrivalRoundNumber?: number | null;
}

export interface ReleaseResultDto {
  playerId: Guid;
  playerName: string;
  clubId: Guid;
  clubName: string;
  releaseCost: number;
  /** Offers the club had on the table for him, withdrawn along with the contract. */
  withdrawnOffers: number;
  message: string;
}

export interface NpcTransferResultDto {
  proposalsMade: number;
  accepted: number;
  rejected: number;
  /** Free agents a club picked up while it was short of the minimum. */
  signed: number;
  completed: number;
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

/**
 * A club's next match, and whether it can be played right now.
 *
 * `waveOpen` is false when the fixture is a cup leg behind a championship of the same day
 * that has not been played yet. That is an answer, not an error, and a screen that offered
 * such a fixture as kick-off time sent the manager to a refusal.
 */
export interface NextFixtureDto {
  fixture: FixtureDto;
  roundId: Guid;
  /**
   * The window's number, which is an identifier and not an order of play: a championship
   * window is numbered after its matchday and a cup window by how many ties there have been.
   */
  roundNumber: number;
  matchDayId?: Guid | null;
  matchDayNumber?: number | null;
  waveOpen: boolean;
  /** The wave the day is in while this fixture is not it. Empty when it is. */
  waitingFor: string;
  /** The edition the window belongs to, which is what a box reads out. */
  competitionName: string;
  /** The kind of competition, which is what orders a matchday's windows. */
  competitionType: string;
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
  /** Whether a goal came from the spot. Decided by the engine when it emitted the goal. */
  fromPenalty: boolean;
  /** What the player on this event is called. */
  playerName?: string | null;
  icon: string;
}

export type MatchHalf = 'First' | 'Second' | 'ExtraTime' | 'PenaltyShootout';

/**
 * The match a club is playing right now, as a navigation badge shows it. Null is "not at the
 * moment", and it is the answer most of a season's hours.
 */
export interface LiveMatchDto {
  matchId: Guid;
  roundId: Guid;
  homeTeamId: Guid;
  homeTeamName: string;
  homeShortName: string;
  homePrimaryColor: string;
  homeSecondaryColor: string;
  awayTeamId: Guid;
  awayTeamName: string;
  awayShortName: string;
  awayPrimaryColor: string;
  awaySecondaryColor: string;
  homeGoals: number;
  awayGoals: number;
  minute: number;
  /** Whether the club the badge was asked about is the home one. */
  isHome: boolean;
  /** The match is standing at the interval, where the clock is stopped rather than late. */
  atHalfTime: boolean;
}

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
  /**
   * The shootout, when the match has gone to penalties, and null on every match that has
   * not. It is null rather than an empty object for the same reason the penalty below is a
   * flag: a match at ninety minutes level is not a match in a shootout, and a screen that
   * cannot tell the two apart would show a penalty that is not being taken.
   */
  shootout: ShootoutDto | null;
}

export interface PenaltyTakerOptionsDto {
  awaitingSelection: boolean;
  candidates: MatchPlayerDto[];
}

/**
 * A shootout as a manager reads it from the stand: the coin, the two orders, the kicks
 * taken and whose turn it is.
 *
 * The numbers are the engine's and the order of the other club is the engine's too. The only
 * decision in here is `awaitingOrder`, and it is a decision the match is holding the clock
 * for — exactly as it holds it for the taker of a penalty.
 */
export interface ShootoutDto {
  homeTeamId: Guid;
  awayTeamId: Guid;
  /** Which club the coin sent to the spot first. */
  homeTakesFirst: boolean;
  /** Whose kick it is, or null once the shootout is over. */
  nextTeamId: Guid | null;
  /** Who walks to the spot next, out of the order that side named. */
  nextTakerId: Guid | null;
  homeGoals: number;
  awayGoals: number;
  homeKicksTaken: number;
  awayKicksTaken: number;
  /** Whether the five kicks each side is given have both been taken. */
  isSuddenDeath: boolean;
  isComplete: boolean;
  winnerTeamId: Guid | null;
  /** While this is set the match is standing at ninety minutes waiting for the manager. */
  awaitingOrder: boolean;
  /**
   * The men the manager's club may still name, with the chance each of them has against the
   * keeper in the other goal. The engine reads it; the screen does not work it out.
   */
  candidates: MatchPlayerDto[];
  /** The order each side named. The one that is not the manager's is the engine's. */
  homeTakers: Guid[];
  awayTakers: Guid[];
  kicks: ShootoutKickDto[];
}

/** One kick of a shootout: who took it, for whom, and whether it went in. */
export interface ShootoutKickDto {
  teamId: Guid;
  takerId: Guid;
  scored: boolean;
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

/**
 * The band a club sits in, tagged by the backend.
 *
 * The divisions do not share a pair of bands: the top one has the title and no promotion race,
 * the middle ones race in both directions, and the bottom one has no division under it — so it
 * promotes four clubs and relegates nobody.
 */
export type TableZone = 'None' | 'Safe' | 'Promotion' | 'Relegation' | 'Champion';

/**
 * How one of a club's finished games went. It is the backend's word, and it is a word rather
 * than a number: the screen paints the result, it does not work out one.
 */
export type MatchOutcome = 'Win' | 'Draw' | 'Loss';

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
  /**
   * The club's last finished games, oldest first, and no longer than the club has played.
   *
   * A club three matchdays in carries three results, and the table shows the two empty places
   * beside them: a run padded to five would be two matches the season has not played yet.
   */
  form?: MatchOutcome[];
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

/**
 * A club's place in a season: which division it is in, and the line it holds there.
 *
 * It is asked of a club rather than of a table, because a club is what a manager opens, and
 * the season is required — a club's division is its enrolment for that season, not a fact
 * about the club, so the same club is in another division next year. `competitionSeasonId`
 * comes back with it so a screen can take the manager to the very table this line is on,
 * and a club in no division of that season comes back with nulls instead of an error: it is a
 * fact about the world, not a failure to read one.
 */
export interface ClubStandingDto {
  teamId: Guid;
  seasonId: Guid;
  competitionSeasonId?: Guid | null;
  /** "4ª Divisão", and empty when the club is in none. */
  divisionName: string;
  /** 1 is the top of the pyramid. Null when the club is in no division. */
  tier?: number | null;
  /**
   * How many clubs the division holds, so a position reads as one of a number: "7º de 12" is a
   * place in a table, and "7º" alone is a place in a list nobody can see.
   */
  clubsInDivision?: number | null;
  /**
   * The strength of the club's squad this season, the same average the division's table is
   * seeded with. It comes from here rather than from the club because a club's strength is a
   * fact about its men and its season, and it is answered even for a club that is in no
   * division at all this season.
   */
  squadStars: number;
  /** The club's own line, and null when the division has not been drawn yet. */
  row?: StandingDto | null;
}

/**
 * One line of a top scorers table.
 *
 * **The order and the numbers that settle it are the backend's.** Goals first, then fewest
 * games, then fewest cards (a yellow is one and a red is three), then oldest — so a screen that
 * sorted its own rows by goals and age would be inventing an order between two players the game
 * says are level, and the three prizes that hang on that order with it. A screen shows the
 * position it is given and the columns that explain it.
 */
export interface ScorerDto {
  playerId: Guid;
  playerName: string;
  age: number;
  goals: number;
  teamId: Guid;
  teamName: string;
  /** The club's colours, so the club beside his name is drawn as its own shield. */
  teamPrimaryColor?: string | null;
  teamSecondaryColor?: string | null;
  /** Where he stands, counted from one, and shared with anyone the chain could not part. */
  position: number;
  /** How many other players share this position, and zero when nobody does. */
  tiedWith: number;
  /** Games he played: started plus came off the bench. The second thing the order looks at. */
  appearances: number;
  yellowCards: number;
  redCards: number;
  /** The two card columns already weighed: a yellow is one point and a red is three. */
  cardPoints: number;
}

export interface MatchEngineEventDto {
  sequence: number;
  minute: number;
  type: string;
  teamId?: Guid | null;
  playerId?: Guid | null;
  icon: string;
  description: string;
  /** Whether a goal came from the spot. Decided by the engine when it emitted the goal. */
  fromPenalty: boolean;
  /** What the player on this event is called, so a scoreline can name him without a squad. */
  playerName?: string | null;
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

/** One leg of a cup tie, as the scoreboard shows it under the score of the other one. */
export interface CupLegResultDto {
  homeTeamId: Guid;
  homeTeamName: string;
  homeGoals: number;
  awayTeamId: Guid;
  awayTeamName: string;
  awayGoals: number;
}

/**
 * Where a match is being played and what kind of match it is: the season and the day, the
 * competition and the phase of it, the ground, and the leg before this one.
 *
 * Every word of it comes from the backend because a client that assembled the phase itself
 * would have to know that a cup window's number is not a round number — and a screen that
 * guesses wrong about a cup guesses wrong about whether there is a return leg.
 */
export interface MatchContextDto {
  seasonName: string;
  matchDayNumber: number;
  competitionName: string;
  competitionType: 'League' | 'Cup' | 'SuperCup';
  editionName: string;
  phaseName: string;
  legLabel: string | null;
  stadiumName: string;
  stadiumCapacity: number;
  firstLeg: CupLegResultDto | null;
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
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
  /**
   * The ceiling on his reading. A profile that could only show what a man is has no way to
   * answer "what will he become", and the backend owns that number like every other one.
   */
  potential: number;
  /**
   * How much football is left in him at minute eighty-five, on the same 1..100 scale as the
   * attributes. The backend sends it on the squad and the player rows alike; it is here
   * because the contract carries it, and the training panel reads it as the reason two men
   * with the same numbers pay different prices for the same session.
   */
  stamina: number;
  stars: number;
  /**
   * Each attribute read as stars, worked out by the backend from the same conversion the
   * engine makes. They arrive beside the attributes on purpose: the card used to divide the
   * raw value by two, which was right while the attributes ran 1..20 and has been wrong on
   * every card since the move to 1..100. A star is never computed here.
   */
  speedStars: number;
  accuracyStars: number;
  dribblingStars: number;
  headingStars: number;
  strengthStars: number;
  goalkeeperPowerStars: number;
  reflexesStars: number;
  seasonId?: Guid | null;
  teamId?: Guid | null;
  teamName: string;
  teamPrimaryColor?: string | null;
  teamSecondaryColor?: string | null;
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
  opponentTeamId?: Guid | null;
  opponentTeamPrimaryColor?: string | null;
  opponentTeamSecondaryColor?: string | null;
  homeGoals: number;
  awayGoals: number;
  roundNumber: number;

  /**
   * The club he played for, and the match read as a fixture — the same words the club page's
   * match table is written in, because a player's history and a club's last matches are the
   * same matches read by a man and by a club.
   */
  teamName?: string | null;
  seasonName?: string | null;
  competitionName?: string | null;
  phaseName?: string | null;
  stadiumName?: string | null;
  attendance?: number | null;
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
  /**
   * The three words a head-to-head is read by, and they are optional because a match played
   * before a competition was named cannot be described by one: the same match is a league
   * game in one season and a cup tie in another, and a screen that printed a guess would be
   * printing a fiction.
   */
  seasonName?: string | null;
  competitionName?: string | null;
  phaseName?: string | null;
  attendance?: number | null;
  stadiumName?: string | null;
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
  /**
   * Which division of the pyramid, counted from one at the top, and null for a competition that
   * is not one of its divisions at all.
   *
   * The name is for reading and this is for deciding: a shelf holding the 1ª and the 3ª has to
   * draw two different cups, and a client that recovered the tier out of the name's leading
   * number would be parsing a label to learn a fact the record already carries.
   */
  divisionTier?: number | null;
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
  /** The sponsor on the shirt, and the deal it was signed on. Null when the club has no deal. */
  current: SponsorOfferDto | null;
  /** Matches of the deal still to be played. Zero is the only moment a change is allowed. */
  matchesLeft: number;
  /** The offers on the table, five of them, which is a shortlist and not a market. */
  candidates: SponsorOfferDto[];
  /** The one the club is carrying, so the card can say who it is at a glance. */
  masterSponsorId: string;
};

/**
 * The manager of a club: the name chosen when the career began, and the club it belongs to.
 * A club has one manager and only one — the career begins once.
 */
export type ManagerDto = {
  id: Guid;
  name: string;
  teamId: Guid;
  /** The user who owns this career, or null for NPC-controlled clubs. */
  userId?: Guid | null;
  startedAt: string;
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
  /** Games he played, which is the second thing the order of this table looks at. */
  appearances: number;
  yellowCards: number;
  redCards: number;
  /** The two card columns already weighed: a yellow is one and a red is three. */
  cardPoints: number;
  /** How many other players of the club share this position, and zero when nobody does. */
  tiedWith: number;
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

/**
 * One season's cup, read as a bracket.
 *
 * Only the rounds that have been drawn are in it, and that is the point rather than a
 * limitation: nobody knows who is in the quarter-finals before the round of 16 has been played,
 * so a bracket that drew them in advance would be showing two clubs in a tie neither has earned.
 */
export interface CupBracketDto {
  competitionSeasonId: string;
  seasonId: string;
  /** The competition's own name, without a tier: a cup has none. */
  competitionName: string;
  rounds: CupBracketRoundDto[];
  /** Who won it, and null while the final is still to be played. */
  championTeamId?: string | null;
  championTeamName?: string | null;
  /** The losing side of the final: the runner-up, which is a fact in its own right. */
  runnerUpTeamName?: string | null;
}

/** One round of the bracket, named in the game's words rather than as a number. */
export interface CupBracketRoundDto {
  roundNumber: number;
  name: string;
  ties: CupBracketTieDto[];
}

/**
 * One tie: two clubs, two legs and the aggregate.
 *
 * `firstLegScore` and `secondLegScore` are the goals of the leg's own home side, which is the
 * tie's home club in the first leg and the tie's away club in the second — the legs swap ends.
 * Every number on a club is that club's own, so a screen reads a tie without working out which
 * end any of it was on.
 */
export interface CupBracketTieDto {
  tieId: string;
  roundNumber: number;
  clubs: CupBracketClubDto[];
  firstLegScore?: number | null;
  secondLegScore?: number | null;
  /** The two matches, so a manager can watch or re-watch either leg. */
  firstLegMatchId?: string | null;
  secondLegMatchId?: string | null;
}

/** One club's line of a tie: who it is, what it scored, and whether it went through. */
export interface CupBracketClubDto {
  teamId: string;
  name: string;
  primaryColor: string;
  secondaryColor: string;
  firstLegGoals?: number | null;
  firstLegConceded?: number | null;
  secondLegGoals?: number | null;
  secondLegConceded?: number | null;
  /** Goals across the two legs: what the tie was decided on. */
  aggregateGoals?: number | null;
  aggregateConceded?: number | null;
  /** What it scored in the shootout, and null when the tie was never level. */
  penaltyGoals?: number | null;
  isWinner: boolean;
  isLoser: boolean;
}

/**
 * What a division's table is paid out of, and what a position in it is worth.
 *
 * The figures come from the backend down to the last club's rounding remainder, because that is
 * the number a manager adds the other eleven up to find. A screen that divided the purse by
 * twelve would publish a championship that does not pay out its own money.
 */
export interface DivisionPurseDto {
  /** Which division, counted from one at the top. */
  tier: number;
  /** The division's own name: "1ª Divisão". */
  name: string;
  /** What the whole table is paid out of at the end of the season. */
  purse: number;
  /** How many clubs share it. */
  clubs: number;
  /** One share per position, the champion's first. */
  shares: PrizeShareDto[];
}

/** What one finishing position in a division is paid. */
export interface PrizeShareDto {
  position: number;
  amount: number;
}

/**
 * What the cup pays: the winner's cheque and the consolation for the round a club went out in.
 *
 * A knockout is paid on the way out, so this is the other half of the money — the championship
 * pays a table's twelve positions and the cup pays a run's six outcomes, and a club can be in
 * both.
 */
export interface CupPrizeDto {
  /** Which tie-round, counted from the round of 16; zero for the champion's cheque. */
  tieRound: number;
  /** The round's name in the game's own words, or "Campeão" for the winner's cheque. */
  name: string;
  amount: number;
  /** Whether this is the winner's cheque rather than a consolation. */
  isChampion: boolean;
}

/**
 * What a competition pays its artilharia: the three shares, and who is holding each of them.
 *
 * It is asked of an edition and not of a season because a season's championship is three
 * editions, each with its own artilharia and its own title to be a share of. A cup has no purse
 * of its own, so each of its three scorers is paid a share of the title of the division his club
 * is in, and each line says whose title that was.
 */
export interface TopScorerPrizeListDto {
  competitionSeasonId: string;
  seasonId: string;
  /** The edition's own name: a division's, or the cup's. */
  competitionName: string;
  /** 1 is the top of the pyramid, and null for a cup. */
  tier?: number | null;
  /** The champion's prize these shares are a part of, and null for a cup. */
  baseAmount?: number | null;
  /** The three shares, first place first, whether or not anybody took them. */
  rates: TopScorerRateDto[];
  winners: TopScorerPrizeDto[];
}

/** One place's share, said on its own so a panel does not have to know the rule. */
export interface TopScorerRateDto {
  place: number;
  /** The share of the champion's prize: 0.1, 0.05 or 0.03. */
  rate: number;
}

/**
 * One man in a competition's artilharia, and what his club is paid for it.
 *
 * `position` and `prizeSlot` are two numbers because they are not always the same: two men
 * level on the whole chain are both second, both take the second prize, and the third prize is
 * paid to nobody. One number would have to choose between calling a man third who is not, and
 * paying a second place less than the prize for second.
 */
export interface TopScorerPrizeDto {
  playerId: Guid;
  playerName: string;
  age: number;
  teamId: Guid;
  teamName?: string | null;
  teamPrimaryColor?: string | null;
  teamSecondaryColor?: string | null;
  position: number;
  prizeSlot: number;
  /** How many other players share this position, and zero when nobody does. */
  tiedWith: number;
  goals: number;
  appearances: number;
  /** The cards already weighed: a yellow is one and a red is three. */
  cardPoints: number;
  /** The share of the champion's prize this place carries. */
  rate: number;
  /**
   * What the club is paid, and null when the competition has no title to take a share of. It is
   * new money: it does not come out of the champion's cheque.
   */
  amount?: number | null;
}

/** Returned by login and register: the JWT and the identity it carries. */
export interface AuthResponseDto {
  token: string;
  userId: Guid;
  email: string;
  teamId?: Guid | null;
  coachName?: string | null;
}

export interface AuthRegisterRequestDto {
  email: string;
  password: string;
  coachName?: string;
}

export interface AuthLoginRequestDto {
  email: string;
  password: string;
}

export interface ChangePasswordRequestDto {
  currentPassword: string;
  newPassword: string;
}

export interface UpdateTeamNameRequestDto {
  name: string;
}

export interface UpdateTeamColorsRequestDto {
  primaryColor: string;
  secondaryColor: string;
}

export interface ClubRankingDto {
  teamId: string;
  teamName: string;
  teamShortName: string;
  primaryColor: string;
  secondaryColor: string;
  managerName: string;
  position: number;
  rankingPoints: number;
  rankingBase: number;
  cupScore: number;
  currentDivision: number;
  currentDivisionName: string;
  strength: number;
  seasonScores: Record<number, number>;
  cupScores: Record<number, number>;
}

/**
 * The club's balance, as the market reads it: the money the club has, and nothing else.
 * It is read from the last line written in the club's whole book.
 */
export interface ClubBalanceDto {
  teamId: Guid;
  teamName: string;
  balance: number;
}

/**
 * The recent business of a division: the transfers that finished in the last few rounds,
 * across every club in it.
 */
export interface DivisionRecentTransfersDto {
  competitionSeasonId: Guid;
  currentRound: number;
  windowRounds: number;
  transfers: TransferHistoryLineDto[];
}

/**
 * A club's transfer history: every deal the club was involved in, across the seasons asked
 * for, newest first. Pending and accepted sit in the same table as completed ones.
 */
export interface ClubTransferHistoryDto {
  teamId: Guid;
  teamName: string;
  seasonNumbers: number[];
  transfers: TransferHistoryLineDto[];
}

/**
 * A message in the manager's box, as the backend wrote it.
 *
 * The body is plain text with its paragraphs separated by a blank line, and it is plain text
 * on purpose: the names inside it are doors because of the `mentions` beside it, so a body
 * that could carry markup would be a place where a club's name stopped being a name. The
 * screen splits the body on the blank line and never rewrites a word of it.
 */
export interface InboxMessageDto {
  id: Guid;
  /** `Finance`, `MatchReport`, `TransferOffer`, `Title` or `Club`, as a name. */
  category: string;
  subject: string;
  senderName: string;
  body: string;
  mentions: InboxPersonDto[];
  linkLabel?: string | null;
  linkRoute?: string | null;
  createdAt: string;
  isRead: boolean;
  readAt?: string | null;
}

/** A name a message uses, and the thing it belongs to, so the screen can make it a door. */
export interface InboxPersonDto {
  name: string;
  /** `player` or `team`. */
  kind: string;
  id: Guid;
}

/**
 * A page of the box, newest first, and the number of messages the manager has not opened.
 *
 * The count travels with the page rather than beside it in a second request: the badge in the
 * column and the page on the screen are the same fact about the same moment, and a count
 * fetched a second later is a badge that disagrees with the mail it is counting.
 */
export interface InboxBoxDto {
  messages: InboxMessageDto[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  unreadCount: number;
}
