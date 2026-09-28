import type { PlayerInfo, Position, TeamInfo } from '@/types';
import type { FeedEvent } from '@/state';

export const NAMES = ['João', 'Pedro', 'Ricardo', 'Danilo', 'Lucas', 'Marcos', 'Carlos', 'Rafael', 'Bruno', 'Gabriel', 'Felipe', 'Diego', 'André', 'Caio', 'Gustavo', 'Henrique', 'Matheus', 'Vinícius', 'Rodrigo', 'Thiago', 'Leonardo', 'Samuel', 'Igor', 'Arthur', 'Murilo', 'Eduardo', 'Renato', 'Vitor', 'Wesley', 'Alex'];

export const SURNAMES = ['Silva', 'Ferraz', 'Machado', 'Pires', 'Costa', 'Moura', 'Ribeiro', 'Almeida', 'Barbosa', 'Teixeira', 'Nunes', 'Campos', 'Vieira', 'Lima', 'Souza', 'Mendes', 'Rocha', 'Cardoso', 'Dias', 'Moreira'];

export const TEAM_COLOR_PALETTES = [
  { primary: '#36c2ff', secondary: '#0b5ea8' },
  { primary: '#ff5b6e', secondary: '#a91f36' },
  { primary: '#43d17a', secondary: '#167a43' },
  { primary: '#ffb84d', secondary: '#b76800' },
  { primary: '#c77dff', secondary: '#6a35a8' },
  { primary: '#00d4b8', secondary: '#08796c' },
  { primary: '#ff78c8', secondary: '#a52f78' },
  { primary: '#9fb4c8', secondary: '#4d657a' },
];

export const FORMATIONS = ['4-4-2', '4-3-3', '4-5-1', '3-5-2', '3-4-3', '5-3-2', '5-4-1', '4-2-3-1'];

let _seed = 0;

export function seedRandom(seed: number) {
  _seed = seed;
  let state = seed;
  const random = () => {
    state = (state * 1103515245 + 12345) & 0x7fffffff;
    return state / 0x7fffffff;
  };
  Math.random = random;
}

export function rand(min: number, max: number): number {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

export function pick<T>(array: T[]): T {
  return array[Math.floor(Math.random() * array.length)];
}

export function weightedAge(): number {
  const r = Math.random();
  if (r < 0.06) return rand(16, 18);
  if (r < 0.34) return rand(19, 21);
  if (r < 0.65) return rand(22, 25);
  if (r < 0.84) return rand(26, 29);
  if (r < 0.94) return rand(30, 33);
  if (r < 0.985) return rand(34, 37);
  return rand(38, 41);
}

export function ageFactor(age: number) {
  return {
    speed: age < 21 ? 1.08 : age < 29 ? 1.03 : age < 34 ? 0.98 : 0.88,
    energy: age < 22 ? 1.1 : age < 29 ? 1.04 : age < 34 ? 0.94 : 0.82,
    accuracy: age < 20 ? 0.84 : age < 27 ? 1.02 : age < 34 ? 1.08 : 1.05,
    strength: age < 20 ? 0.86 : age < 28 ? 1.02 : age < 35 ? 1.07 : 0.98,
  };
}

export function goaliePower(p: PlayerInfo): number {
  if (p.position === 'GK') {
    return Math.max(1, Math.round((p.speed + p.strength + p.accuracy) / 3));
  }
  return Math.max(1, Math.round((p.speed + p.strength + p.accuracy) / 3));
}

/**
 * The order players are read in everywhere: goalkeepers, defenders, midfielders and
 * attackers, and by name inside a position. The API already returns its lists in this
 * order, but a client that sorts its own list has to use the same rule or the two
 * disagree on screen.
 */
const POSITION_ORDER: Position[] = ['GK', 'DEF', 'MID', 'ATT'];

export function positionRank(position: string): number {
  const index = POSITION_ORDER.indexOf(position as Position);
  return index < 0 ? POSITION_ORDER.length : index;
}

export function sortByPosition<T extends { position: string; name: string }>(players: T[]): T[] {
  return [...players].sort(
    (a, b) => positionRank(a.position) - positionRank(b.position) || a.name.localeCompare(b.name, 'pt-BR')
  );
}

export function positionLabel(pos: string): string {
  return pos === 'GK' ? 'GOL' : pos === 'DEF' ? 'ZAG' : pos === 'MID' ? 'MEI' : 'ATA';
}

export function attrLine(p: PlayerInfo): string {
  const attrs = [];
  if (p.position !== 'GK') {
    attrs.push(`Vel ${p.speed}`, `Des ${p.accuracy}`, `Dri ${p.dribbling}`, `Cab ${p.heading}`, `For ${p.strength}`);
  } else {
    attrs.push(`Gol ${p.goalkeeperPower}`, `Ref ${p.reflexes}`, `Vel ${p.speed}`);
  }
  return attrs.join(' • ');
}

export function cardSymbols(p: PlayerInfo): string {
  let s = '';
  if (p.matchYellowCards && p.matchYellowCards > 0) s += '🟨'.repeat(p.matchYellowCards);
  if (p.redCard) s += '🟥';
  const susp = p.suspensionRounds || p.suspensionMatches || 0;
  if (susp > 0) s += ` 🚫${susp}`;
  if (p.injury) s += ' 🩹';
  return s;
}

export function teamAvailabilityLabel(p: PlayerInfo): string {
  if (p.injuryRoundsRemaining && p.injuryRoundsRemaining > 0) return `🩹 Lesionado • ${p.injuryRoundsRemaining} rodada(s)`;
  if (p.suspensionRounds && p.suspensionRounds > 0) return `🚫 Suspenso • ${p.suspensionRounds} rodada(s)`;
  return 'Disponível';
}

export function isPlayerUnavailable(p: PlayerInfo): boolean {
  const hasInjury = p.injury && p.injuryRoundsRemaining && p.injuryRoundsRemaining > 0;
  const hasSuspension = (p.suspensionRounds || p.suspensionMatches || 0) > 0;
  return !!hasInjury || !!hasSuspension;
}

export function energyClass(energy: number): string {
  if (energy < 35) return 'energy-red';
  if (energy < 70) return 'energy-yellow';
  return 'energy-green';
}

export function energyPercent(energy: number): string {
  return `${Math.max(0, Math.min(100, energy))}%`;
}

/**
 * Converts a numeric star rating (0-5, in 0.5 increments) to a string of Unicode stars.
 * e.g., 3.5 -> "★★★⯨", 4.0 -> "★★★★"
 */
export function starsToString(stars: number): string {
  const full = Math.floor(stars);
  const hasHalf = stars - full >= 0.5;
  let result = '★'.repeat(full);
  if (hasHalf) result += '⯨';
  return result;
}

/**
 * Converts a single attribute value (1..20) to stars (0.5..5.0).
 * Matches backend PlayerRating.AttributeToStars.
 */
export function attributeToStars(attribute: number): number {
  if (attribute <= 0) return 0;
  if (attribute <= 2) return 0.5;
  const stars = Math.ceil(attribute / 2) * 0.5;
  return Math.min(stars, 5.0);
}

/**
 * Rounds a value to the nearest 0.5 increment, clamped to 0..5.
 * Matches backend PlayerRating.RoundToHalfStar.
 */
function roundToHalfStar(value: number): number {
  const rounded = Math.round(value * 2) / 2;
  return Math.max(0, Math.min(5, rounded));
}

/**
 * Calculates stars for a single player from their attributes.
 * Matches backend PlayerRating.CalculateOutfieldStars / CalculateGoalkeeperStars.
 */
export function calculatePlayerStars(player: { position: string; speed: number; accuracy: number; dribbling: number; heading: number; strength: number; goalkeeperPower?: number; reflexes?: number }): number {
  const isGK = player.position === 'GK';
  
  if (isGK) {
    const sum = attributeToStars(player.speed || 0)
      + attributeToStars(player.accuracy || 0)
      + attributeToStars(player.goalkeeperPower || 0)
      + attributeToStars(player.reflexes || 0)
      + attributeToStars(player.strength || 0);
    return roundToHalfStar(sum / 5);
  } else {
    const sum = attributeToStars(player.speed || 0)
      + attributeToStars(player.accuracy || 0)
      + attributeToStars(player.dribbling || 0)
      + attributeToStars(player.heading || 0)
      + attributeToStars(player.strength || 0);
    return roundToHalfStar(sum / 5);
  }
}

/**
 * Calculates team stars as the average of all players' stars.
 * Matches backend PlayerRating.CalculateTeamStars.
 */
export function calculateTeamStars(players: Array<{ position: string; speed: number; accuracy: number; dribbling: number; heading: number; strength: number; goalkeeperPower?: number; reflexes?: number }>): number {
  if (!players || players.length === 0) return 0;
  
  let total = 0;
  for (const player of players) {
    total += calculatePlayerStars(player);
  }
  
  return roundToHalfStar(total / players.length);
}

/**
 * What each event looks like in the feed. The engine sends a stable key and the client
 * decides how to draw it, so a new event type never arrives as a blank row.
 */
const EVENT_ICONS: Record<string, string> = {
  // A team sheet is a list of names, so it is shown as one.
  LineupAnnounced: '📋',
  KickOff: '▶',
  // Most of a football match is the ball being played, and it says so here: a pass and a
  // ball won are the two beats that make a goal mean something when it arrives.
  BuildUp: '⚽',
  Shot: '🎯',
  Save: '🧤',
  GoalScored: '⚽',
  OwnGoalScored: '🔴',
  Corner: '🚩',
  Foul: '🦵',
  YellowCardShown: '🟨',
  RedCardShown: '🟥',
  PlayerInjured: '🚑',
  KeeperPromoted: '🧤',
  SubstitutionMade: '🔁',
  PenaltyAwarded: '🎯',
  PenaltyTaken: '🎯',
  PenaltySaved: '🧤',
  StoppageTimeAdded: '⏱',
  HalfTimeReached: '⏸',
  SecondHalfStarted: '▶',
  // A tie level after ninety minutes is not the end of the match: the clock stops, the
  // whistle goes, and the two sides go to the spot.
  FullTimeReached: '⏱',
  PenaltyShootoutStarted: '⚽',
  PenaltyShootoutKick: '🎯',
  MatchFinished: '⏹'
};

export function eventIcon(type: string, fallback: string): string {
  return EVENT_ICONS[type] ?? fallback ?? '•';
}

export function convertToFeedEvent(event: any): FeedEvent {
  return {
    sequence: event.sequence,
    minute: event.minute,
    description: event.description,
    icon: eventIcon(event.type, event.icon),
    type: event.type,
    teamId: event.teamId,
    playerId: event.playerId,
    homeScore: event.homeScore,
    awayScore: event.awayScore,
    onTarget: event.type === 'Shot' || event.type === 'Save',
    isGoal: event.type === 'GoalScored' || event.type === 'OwnGoalScored',
    scored: event.type === 'PenaltyShootoutKick' ? event.icon === 'goal' : null,
  };
}

export function isGKUnavailableForSub(p: PlayerInfo): boolean {
  if (p.position !== 'GK' && !p.emergencyGK) return true;
  return isPlayerUnavailable(p);
}
