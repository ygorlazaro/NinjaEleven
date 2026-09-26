import React from 'react';
import type { FeedEvent } from '@/state';

interface MatchFeedProps {
  feed: FeedEvent[];
  homeColor?: string;
  awayColor?: string;
}

const MatchFeed: React.FC<MatchFeedProps> = ({ feed, homeColor, awayColor }) => {
  if (feed.length === 0) {
    return <div className="league-empty">Aguardando início da partida...</div>;
  }

  return (
    <>
      {feed.map((event) => {
        const teamClass = event.isGoal ? 'team-event' : event.type === 'YellowCardShown' || event.type === 'RedCardShown' ? 'user-event' : '';

        return (
          <div
            key={event.sequence}
            className={`feed-row ${teamClass}`}
            style={teamClass === 'team-event' ? { '--team-primary': homeColor, '--team-secondary': homeColor } as React.CSSProperties : {}}
          >
            <div className="minute">{event.minute}'</div>
            <div className="feed-icon">{event.icon}</div>
            <div className="feed-text">
              {event.description}
              {event.homeScore !== undefined && event.homeScore !== null && event.awayScore !== undefined && event.awayScore !== null && (
                <b> {event.homeScore} × {event.awayScore}</b>
              )}
            </div>
          </div>
        );
      })}
    </>
  );
};

export default MatchFeed;
