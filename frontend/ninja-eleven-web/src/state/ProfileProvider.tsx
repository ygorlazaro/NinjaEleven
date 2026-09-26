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
  | { kind: 'team'; teamId: string }
  | { kind: 'player-over-team'; playerId: string; returnTo: string }
  | null;

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
  const [overlay, setOverlay] = useState<Overlay>(null);

  const close = useCallback(() => setOverlay(null), []);

  const openPlayer = useCallback((playerId: string) => {
    setOverlay({ kind: 'player', playerId });
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
        setOverlay(null);
        navigate(`/team/${teamId}`);
        return;
      }

      setOverlay({ kind: 'team', teamId });
    },
    [managedTeamId, navigate]
  );

  /**
   * A club opened from inside a player keeps the player underneath, so closing the club puts
   * the manager back where the club name was clicked.
   */
  const openTeamUnderPlayer = useCallback(
    (playerId: string) => (teamId: string) => {
      if (managedTeamId && teamId === managedTeamId) {
        setOverlay(null);
        navigate(`/team/${teamId}`);
        return;
      }

      setOverlay({ kind: 'player-over-team', playerId, returnTo: teamId });
    },
    [managedTeamId, navigate]
  );

  const value = useMemo(() => ({ openPlayer, openTeam, close }), [openPlayer, openTeam, close]);

  return (
    <ProfileContext.Provider value={value}>
      {children}

      {overlay?.kind === 'player' && (
        <PlayerProfileModal
          playerId={overlay.playerId}
          onClose={close}
          onOpenTeam={openTeamUnderPlayer(overlay.playerId)}
        />
      )}

      {overlay?.kind === 'team' && (
        <TeamProfileModal teamId={overlay.teamId} onClose={close} />
      )}

      {overlay?.kind === 'player-over-team' && (
        <>
          <PlayerProfileModal
            playerId={overlay.playerId}
            onClose={close}
            onOpenTeam={openTeamUnderPlayer(overlay.playerId)}
          />
          <TeamProfileModal
            teamId={overlay.returnTo}
            onClose={() => setOverlay({ kind: 'player', playerId: overlay.playerId })}
          />
        </>
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
