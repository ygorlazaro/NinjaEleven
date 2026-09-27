import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGameState } from '@/state';
import PlayerProfileModal from '@/components/Match/PlayerProfileModal';

/**
 * Who is being looked at, and what opened them.
 *
 * A player can be reached from anywhere — a table, a scoreline, a sentence in
 * the feed — and the rule is that a name is always a door.
 */
type Overlay =
  | { kind: 'player'; playerId: string };

/**
 * A player looked up from inside a match or club. They are a stack rather than a set of
 * special cases: what is underneath is whatever was open when this was opened, so closing
 * the top one always puts the manager back exactly where the name he clicked was.
 */
const MAX_DEPTH = 8;

/** Puts a frame on top of the stack and keeps the stack from growing without end. */
const push = (stack: Overlay[], frame: Overlay): Overlay[] => {
  const next = [...stack, frame];

  return next.length > MAX_DEPTH ? next.slice(next.length - MAX_DEPTH) : next;
};

interface ProfileContextValue {
  /** Opens a player. This is what a name in the game is wired to. */
  openPlayer: (playerId: string) => void;
  /** Opens a club page (navigates to /team/:teamId). */
  openTeam: (teamId: string) => void;
  close: () => void;
}

const ProfileContext = createContext<ProfileContextValue | null>(null);

export const ProfileProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const navigate = useNavigate();
  const managedTeamId = useGameState((s) => s.selectedTeam?.id);
  const [overlays, setOverlays] = useState<Overlay[]>([]);

  const overlay = overlays[overlays.length - 1] ?? null;

  const close = useCallback(
    () => setOverlays(stack => stack.slice(0, Math.max(0, stack.length - 1))),
    []
  );

  const openPlayer = useCallback((playerId: string) => {
    setOverlays(stack => push(stack, { kind: 'player', playerId }));
  }, []);

  const openTeam = useCallback(
    (teamId: string) => {
      navigate(`/team/${teamId}`);
    },
    [navigate]
  );

  const value = useMemo(() => ({ openPlayer, openTeam, close }), [openPlayer, openTeam, close]);

  return (
    <ProfileContext.Provider value={value}>
      {children}

      {overlay?.kind === 'player' && (
        <PlayerProfileModal
          playerId={overlay.playerId}
          onClose={close}
          onOpenTeam={openTeam}
        />
      )}
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
