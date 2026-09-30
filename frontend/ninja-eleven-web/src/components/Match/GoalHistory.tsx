import React from 'react';
import type { FeedEvent } from '@/state';
import type { MatchLineupDto, MatchPlayerDto } from '@/types';

/**
 * The goals a club has scored, written under its name on the scoreline.
 *
 * A score says how many; it does not say who, and a manager watching a striker's afternoon
 * reads the names rather than the arithmetic. Everything on this strip is read off the
 * events the engine emitted — the scorer, the minute and how the goal was scored all arrive
 * with the goal — so nothing here decides a fact about the match. The one thing added on the
 * client is the grouping: two goals by the same man are one line, because a scoreline that
 * printed his name twice reads as two players.
 */

/** One goal as the scoreline draws it. */
type ScoredGoal = {
  minute: number;
  ownGoal: boolean;
  fromPenalty: boolean;
};

/** One line of the strip: a man and every goal of his, in the order he scored them. */
type ScorerLine = {
  playerId: string;
  name: string;
  goals: ScoredGoal[];
  /** A goal against is a man's error, so it is the line that wears red. */
  ownGoal: boolean;
  fromPenalty: boolean;
};

/**
 * The half a minute belongs to. A goal at 45 is still the first half, which is the same
 * reading the interval itself uses: the second half starts after it.
 */
const FIRST_HALF_END = 45;

const halfOf = (minute: number) => (minute <= FIRST_HALF_END ? '1T' : '2T');

/**
 * The name of every man on the two team sheets, used only as a fallback.
 *
 * A scoreline should not have to find a scorer to name him: the goal carries the name of the
 * man who scored it, because the club he played for is not necessarily the club he is in now,
 * and a finished match is answered with the squad as it is today — a striker who has since
 * left was a goal with a blank beside it. The team sheet fills the gap for a live match where
 * an older event predates the name.
 */
function namesOf(lineup: MatchLineupDto): Map<string, string> {
  const everyone = [
    ...lineup.homeLineup,
    ...lineup.homeBench,
    ...lineup.awayLineup,
    ...lineup.awayBench,
  ] as MatchPlayerDto[];

  return new Map(everyone.map(player => [player.playerId, player.name]));
}

/**
 * The goals of one club, in the order they were scored and grouped by the man who scored
 * them. An own goal belongs to the club it was handed to — the engine names the club that
 * benefited — and is grouped by the defender who put it in his own net, which is the one
 * name a manager looking at a 1 x 0 needs to be able to find.
 */
export function scorerLines(
  feed: FeedEvent[],
  teamId: string | undefined,
  names: Map<string, string>,
): ScorerLine[] {
  if (!teamId) return [];

  const order: string[] = [];
  const byPlayer = new Map<string, ScorerLine>();

  for (const event of feed) {
    if (!event.isGoal || event.teamId !== teamId) continue;
    if (event.playerId === null || event.playerId === undefined) continue;

    const playerName = event.playerName ?? names.get(event.playerId) ?? '';

    const goal: ScoredGoal = {
      minute: event.minute,
      ownGoal: event.type === 'OwnGoalScored',
      fromPenalty: event.fromPenalty === true,
    };

    const existing = byPlayer.get(event.playerId);
    if (existing) {
      existing.goals.push(goal);
      existing.ownGoal = existing.ownGoal || goal.ownGoal;
      existing.fromPenalty = existing.fromPenalty || goal.fromPenalty;
      continue;
    }

    order.push(event.playerId);
    byPlayer.set(event.playerId, {
      playerId: event.playerId,
      name: playerName,
      goals: [goal],
      ownGoal: goal.ownGoal,
      fromPenalty: goal.fromPenalty,
    });
  }

  return order
    .map(id => byPlayer.get(id))
    .filter((line): line is ScorerLine => line !== undefined);
}

type GoalHistoryProps = {
  feed: FeedEvent[];
  teamId?: string;
  lineup: MatchLineupDto;
};

/**
 * The strip under one club's name. Empty renders nothing at all rather than an empty box,
 * so a club that has not scored does not carry a frame where its name used to be.
 */
export default function GoalHistory({ feed, teamId, lineup }: GoalHistoryProps) {
  const lines = scorerLines(feed, teamId, namesOf(lineup));

  if (lines.length === 0) return null;

  return (
    <ul className="goal-history">
      {lines.map(line => (
        <li key={line.playerId} className={line.ownGoal ? 'goal-history__line--own' : undefined}>
          <span className={`ball ${line.ownGoal ? 'ball--own' : 'ball--scored'}`} aria-hidden="true">
            ⚽
          </span>
          <span className="goal-history__name">{line.name}</span>
          <span className="goal-history__minutes">
            {line.goals.map((goal, index) => (
              // The separator is an element, not a join: joining these would stringify each
              // one of them, and a React element stringifies to "[object Object]".
              <React.Fragment key={`${line.playerId}-${index}`}>
                {index > 0 && ', '}
                {line.ownGoal && <span className="goal-history__own-tag">contra</span>}
                {goal.fromPenalty && <span className="goal-history__penalty-tag">pênalti</span>}
                {goal.minute}' {halfOf(goal.minute)}
              </React.Fragment>
            ))}
          </span>
        </li>
      ))}
    </ul>
  );
}