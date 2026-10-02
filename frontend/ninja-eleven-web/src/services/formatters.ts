import type { PlayerInfo, Position } from '@/types';
import type { FeedEvent } from '@/state';

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

/**
 * The same bands as {@link energyClass}, drawn as coloured text rather than as a bar.
 *
 * <p>
 * It is a second function and not an argument because the two answers are two different
 * classes, and a function that took the suffix as a parameter would be called once per screen
 * with the same suffix anyway. What must not happen is four screens each carrying their own
 * copy of the bands: they agreed by accident, and the day one of them was edited the game
 * would be reading a tired player in two colours on two screens. The bands live here, beside
 * <c>energyClass</c>, which is the bar that says the same thing.
 * </p>
 */
export function energyTextClass(energy: number): string {
  if (energy < 35) return 'energy-red-text';
  if (energy < 70) return 'energy-yellow-text';
  return 'energy-green-text';
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
 * The colour an attribute is drawn in: red below average, amber around it, green above, on
 * the 1..100 scale the attributes live on.
 *
 * This lives here and not on each screen because it is read in three of them — the card, the
 * squad table and the transfer list — and a number a manager reads in more than one place is
 * one number, so the places must not each hold their own copy. They were copied, and the
 * bands were 8 and 14 from the days when the scale ran to twenty: kept as they were they put
 * every attribute in the world above fourteen and so every man in green.
 *
 * It is the class a value maps to and nothing more. The value itself arrives from the
 * backend, and so does the star the card draws beside it.
 */
export function attributeToneClass(attribute: number): string {
  if (attribute < 35) return 'attr-red';
  if (attribute < 65) return 'attr-yellow';
  return 'attr-green';
}

/**
 * The colour a match rating is drawn in, on the 0..10 scale a rating lives on.
 *
 * It is a second rule beside `attributeToneClass` and not a call into it, and the two scales
 * have nothing to do with each other: 8.0 is a good evening out of ten and the worst half of
 * an average squad's players out of a hundred, and a function that served both would have to
 * be told which one it was holding. Sharing the rule would be sharing a number, and a 7 on
 * the wrong side of that would colour every striker in a league green.
 *
 * The band itself arrives on the DTO, so this is only ever a lookup — the client is not
 * deciding where a good evening starts, it is dressing the answer the backend gave. The
 * fallback is the same table, written out, so a payload from a server that has not been
 * redeployed still reads correctly rather than falling back to a default colour.
 *
 * Those three numbers are `MatchRules.RatingRedBelow` (6.0), `MatchRules.RatingGreen` (8.0)
 * and `MatchRules.RatingDiamond` (10.0), and the red line is deliberately *not* the ordinary
 * mark: the backend starts a man at `MatchRules.RatingBaseline` (7.0), so raising the
 * ordinary mark to seven left this table exactly as it was. Were it ever restated here from
 * the baseline instead of from these three, the same file would be saying two different
 * things about where red begins.
 */
export function matchRatingClass(rating: number | null | undefined, band?: string): string {
  if (rating === null || rating === undefined) return 'rating-unrated';

  switch (band) {
    case 'Red':
      return 'rating-red';
    case 'Yellow':
      return 'rating-yellow';
    case 'Green':
      return 'rating-green';
    case 'Diamond':
      return 'rating-diamond';
    default:
      break;
  }

  if (rating >= 10) return 'rating-diamond';
  if (rating >= 8) return 'rating-green';
  if (rating >= 6) return 'rating-yellow';
  return 'rating-red';
}

/**
 * The rating as it is printed, with the diamond spelled rather than drawn, so a number that
 * means "the best match anybody had" is not the same shape as the nine beside it.
 */
export function matchRatingText(rating: number | null | undefined): string {
  if (rating === null || rating === undefined) return '—';
  return rating >= 10 ? '💎 10.0' : rating.toFixed(1);
}

/**
 * How full an attribute bar is, as a CSS width on the 1..100 scale the attributes live on.
 *
 * The one thing a bar is allowed to do on the client: map a value the backend sent onto the
 * width of a track. It is not the star conversion -- that belongs to the backend and arrives
 * on the DTO -- and it is not a second scale: an attribute bar used to divide by twenty, so
 * every man in a world of 1..100 attributes drew a bar at least half full and a good one
 * drew a full one.
 */
export function attributeBarWidth(attribute: number): string {
  const value = Number.isFinite(attribute) ? attribute : 0;
  return `${Math.max(0, Math.min(100, value))}%`;
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
    fromPenalty: event.fromPenalty === true,
    playerName: event.playerName ?? null,
    scored: event.type === 'PenaltyShootoutKick' ? event.icon === 'goal' : null,
  };
}

export function isGKUnavailableForSub(p: PlayerInfo): boolean {
  if (p.position !== 'GK' && !p.emergencyGK) return true;
  return isPlayerUnavailable(p);
}
