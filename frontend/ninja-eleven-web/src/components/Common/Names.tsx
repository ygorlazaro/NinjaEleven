import React from 'react';
import { useProfiles } from '@/state/ProfileProvider';

/**
 * A player's name, and a door.
 *
 * It renders as plain text to anything that is not looking for a role — a table cell, a
 * title, a list of names — so the only difference a name ever makes on screen is that it
 * can be clicked. That is the whole contract: a manager who sees a name anywhere in the
 * game can ask who that is.
 */
export const PlayerName: React.FC<{
  playerId: string;
  children: React.ReactNode;
  className?: string;
}> = ({ playerId, children, className }) => {
  const { openPlayer } = useProfiles();

  return (
    <button
      type="button"
      className={`name-link ${className ?? ''}`}
      onClick={event => {
        event.stopPropagation();
        openPlayer(playerId);
      }}
    >
      {children}
    </button>
  );
};

/**
 * A club's name, and a door. Same rule as a player: a club seen anywhere leads to that
 * club, whether it is a row of the table, the opponent on the scoreboard, or a name inside
 * a sentence of the feed.
 */
export const ClubName: React.FC<{
  teamId: string;
  children: React.ReactNode;
  className?: string;
}> = ({ teamId, children, className }) => {
  const { openTeam } = useProfiles();

  return (
    <button
      type="button"
      className={`name-link club-link ${className ?? ''}`}
      onClick={event => {
        event.stopPropagation();
        openTeam(teamId);
      }}
    >
      {children}
    </button>
  );
};
