import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/state/auth';
import { useGameState } from '@/state';

const StartScreen: React.FC = () => {
  const navigate = useNavigate();
  const teamId = useAuthStore((s) => s.teamId);
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);

  useEffect(() => {
    // If user has a club assigned, navigate to it
    if (teamId) {
      navigate(`/team/${teamId}`, { replace: true });
    }
  }, [teamId, navigate]);

  return (
    <div className="card start">
      <h1>Carregando...</h1>
      <p>Atribuindo seu clube automaticamente...</p>
    </div>
  );
};

export default StartScreen;