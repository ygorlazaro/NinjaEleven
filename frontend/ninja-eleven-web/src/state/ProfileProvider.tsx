import React, { createContext, useCallback, useContext, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGameState } from '@/state';

/**
 * Who is being looked at, and what opened them.
 *
 * A player can be reached from anywhere — a table, a scoreline, a sentence in
 * the feed — and the rule is that a name is always a door.
 */
interface ProfileContextValue {
  /** Opens a player. This is what a name in the game is wired to. */
  openPlayer: (playerId: string) => void;
  /** Opens a club page (navigates to /team/:teamId). */
  openTeam: (teamId: string) => void;
}

const ProfileContext = createContext<ProfileContextValue | null>(null);

export const ProfileProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const navigate = useNavigate();
  const managedTeamId = useGameState((s) => s.selectedTeam?.id);

  const openPlayer = useCallback((playerId: string) => {
    navigate(`/player/${playerId}`);
  }, [navigate]);

  const openTeam = useCallback(
    (teamId: string) => {
      navigate(`/team/${teamId}`);
    },
    [navigate]
  );

  const value = useMemo(() => ({ openPlayer, openTeam }), [openPlayer, openTeam]);

  return (
    <ProfileContext.Provider value={value}>
      {children}
    </ProfileContext.Provider>
  );
};

export const useProfiles = (): ProfileContextValue => {
  const context = useContext(ProfileContext);

  if (!context) {
    throw new Error('useProfiles has to be used inside a ProfileProvider.');
  }

  return context;
};