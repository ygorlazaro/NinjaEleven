import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { TeamApi } from '@/api';
import { useAuthStore } from '@/state/auth';
import { useGameState } from '@/state';
import LoginScreen from '@/pages/LoginScreen';
import StartScreen from '@/pages/StartScreen';

/**
 * The root: if the user is not logged in, shows the login screen. If logged in but
 * has no club, shows the StartScreen so they can claim one. If logged in with a club,
 * loads the team into game state and navigates to the team page.
 *
 * The check is on the token, not on a remembered club — the backend owns the club,
 * and a browser that merely remembers one is not a basis for the game.
 */
export default function AuthRoot() {
  const token = useAuthStore((s) => s.token);
  const teamId = useAuthStore((s) => s.teamId);
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);

  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!token || !teamId) return;

    // If the game state already knows the team, no need to re-fetch.
    if (selectedTeam?.id === teamId) return;

    let cancelled = false;
    setLoading(true);

    TeamApi.get(teamId)
      .then(loaded => {
        if (cancelled) return;
        setSelectedTeam(loaded);
        setLeagueTeams([loaded]);
      })
      .catch(() => {
        if (!cancelled) useAuthStore.getState().clearAuth();
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [token, teamId, selectedTeam?.id]);

  if (!token) {
    return <LoginScreen />;
  }

  if (teamId && selectedTeam?.id !== teamId) {
    if (loading) return <div className="card start"><p>Carregando...</p></div>;
    return null;
  }

  if (teamId) {
    return <Navigate to={`/team/${teamId}`} replace />;
  }

  return <StartScreen />;
}
