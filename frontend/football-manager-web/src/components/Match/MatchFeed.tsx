import React, { useMemo } from 'react';
import type { FeedEvent } from '@/state';

interface MatchFeedProps {
  feed: FeedEvent[];
  homeColor?: string;
  awayColor?: string;
}

/**
 * The match feed, newest on top. The store keeps the events in the order the engine
 * emitted them, because that is the order the sequence runs in; a match is read from the
 * present backwards, so the list is reversed here instead of being stored backwards.
 */
const MatchFeed: React.FC<MatchFeedProps> = ({ feed, homeColor, awayColor }) => {
  const events = useMemo(
    () => [...feed].sort((a, b) => b.sequence - a.sequence),
    [feed]
  );

  if (events.length === 0) {
    return <div className="league-empty">Aguardando início da partida...</div>;
  }

  return (
    <>
      {events.map((event) => {
        const teamClass = event.isGoal
          ? 'team-event'
          : event.type === 'YellowCardShown' || event.type === 'RedCardShown'
            ? 'user-event'
            : '';

        return (
          <div
            key={event.sequence}
            className={`feed-row ${teamClass}`}
            style={
              teamClass === 'team-event'
                ? ({ '--team-primary': homeColor, '--team-secondary': homeColor } as React.CSSProperties)
                : {}
            }
          >
            <div className="minute">{event.minute}'</div>
            <div className="feed-icon">{event.icon}</div>
            <div className="feed-text">
              {event.description}
              {event.homeScore !== undefined &&
                event.homeScore !== null &&
                event.awayScore !== undefined &&
                event.awayScore !== null && (
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
