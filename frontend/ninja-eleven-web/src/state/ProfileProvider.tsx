import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGameState } from '@/state';
import PlayerProfileModal from '@/components/Match/PlayerProfileModal';
import TeamProfileModal from '@/components/Match/TeamProfileModal';

/**
 * Who is being looked at, and what opened them.
 *
 * A player and a club can be reached from anywhere — a table, a scoreline, a sentence in
 * the feed — and the rule is that a name is always a door. Handling that with a piece of
 * state in every screen would have been twenty modals wired by hand, and twenty chances for
 * one of them to be the screen where a name is not clickable.
 *
 * The club is a modal and the player's own club is a full screen: a manager looks another
 * club up and comes straight back to the match he was reading, and he manages his own club
 * properly, with the whole squad in front of him.
 */
type Overlay =
  | { kind: 'player'; playerId: string }
  | { kind: 'team'; teamId: string };

/**
 * A club looked up from inside a club, and a player looked up from inside either. They are
 * a stack rather than a set of special cases: what is underneath is whatever was open when
 * this was opened, so closing the top one always puts the manager back exactly where the
 * name he clicked was. The alternative — one state per combination of what is on top of what
 * — is four variants today and an unknowable number the day somebody opens a player from a
 * club that was opened from a player.
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
  /** Opens a club: a modal for somebody else's, the full screen for ours. */
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

  /**
   * Somebody else's club is a modal; ours is a screen. A manager looks another club up from
   * inside a match and comes straight back to the minute he was reading, but he manages his
   * own club properly, with the whole squad in front of him — which is where the root of the
   * game now sends him.
   */
  const openTeam = useCallback(
    (teamId: string) => {
      if (managedTeamId && teamId === managedTeamId) {
        setOverlays([]);
        navigate(`/team/${teamId}`);
        return;
      }

      setOverlays(stack => push(stack, { kind: 'team', teamId }));
    },
    [managedTeamId, navigate]
  );

  const value = useMemo(() => ({ openPlayer, openTeam, close }), [openPlayer, openTeam, close]);

  return (
    <ProfileContext.Provider value={value}>
      {children}

      {/* Only the top of the stack is drawn. What is underneath it is what closing brings
          back, and drawing it as well would put two full-screen panels on top of each
          other for a manager who cannot see the one he came from. */}
      {overlay?.kind === 'player' && (
        <PlayerProfileModal
          playerId={overlay.playerId}
          onClose={close}
          onOpenTeam={openTeam}
        />
      )}

      {overlay?.kind === 'team' && (
        <TeamProfileModal teamId={overlay.teamId} onClose={close} />
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
