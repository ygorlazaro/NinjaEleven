import React, { useMemo } from 'react';
import type { FeedEvent } from '@/state';
import NarrativeText, { buildNameIndex } from './NarrativeText';

interface MatchFeedProps {
  feed: FeedEvent[];
  homeColor?: string;
  awayColor?: string;
  /** The club the manager is watching, which is the one whose moments are marked. */
  managerTeamId?: string | null;
  homeTeamId?: string | null;
  awayTeamId?: string | null;
  /**
   * The two clubs and the men on the pitch, so the names inside a sentence can be doors.
   * Without them the feed is prose and prose is the one place a manager most wants to
   * click a name.
   */
  nameIndex?: ReturnType<typeof buildNameIndex> | null;
}

/**
 * The match feed, newest on top. The store keeps the events in the order the engine
 * emitted them, because that is the order the sequence runs in; a match is read from the
 * present backwards, so the list is reversed here instead of being stored backwards.
 */
const MatchFeed: React.FC<MatchFeedProps> = ({
  feed,
  homeColor,
  awayColor,
  managerTeamId,
  homeTeamId,
  awayTeamId,
  nameIndex = null,
}) => {
  const events = useMemo(
    () => [...feed].sort((a, b) => b.sequence - a.sequence),
    [feed]
  );

  if (events.length === 0) {
    return <div className="league-empty">Aguardando início da partida...</div>;
  }

  /**
   * The colour of the club the event belongs to, and not the home colour for everything:
   * an away goal is the away club's moment, and painting it in the home colours is a small
   * lie told sixty times a match.
   */
  const colorOf = (teamId?: string | null): string | undefined => {
    if (!teamId) return undefined;
    if (teamId === homeTeamId) return homeColor;
    if (teamId === awayTeamId) return awayColor;
    return undefined;
  };

  return (
    <>
      {events.map((event) => {
        // The manager's own club is the one worth finding in a column of ninety rows, so
        // its moments are marked — and only its moments, or the marking says nothing.
        const isManagedClub = !!managerTeamId && event.teamId === managerTeamId;
        const color = isManagedClub ? colorOf(event.teamId) : undefined;

        const rowClass = [
          'feed-row',
          event.isGoal ? 'goal-event' : '',
          isManagedClub ? 'managed-event' : '',
        ]
          .filter(Boolean)
          .join(' ');

        return (
          <div
            key={event.sequence}
            className={rowClass}
            style={
              color
                ? ({ '--team-primary': color, '--team-secondary': color } as React.CSSProperties)
                : {}
            }
          >
            <div className="minute">{event.minute}'</div>
            <div className="feed-icon">{event.icon}</div>
            <div className="feed-text">
              <NarrativeText text={event.description} index={nameIndex} />
              {/*
                The score belongs to a goal and to nothing else. The engine sends the
                running score on every event so a client can always rebuild it, which is not
                a reason to print it beside a pass: a feed where every line ends in the same
                two numbers is a feed nobody reads.
              */}
              {event.isGoal && (
                <b>
                  {' '}
                  {event.homeScore} × {event.awayScore}
                </b>
              )}
            </div>
          </div>
        );
      })}
    </>
  );
};

export default MatchFeed;
