import React, { useEffect, useState } from 'react';
import { useGameState } from '@/state';
import type { TeamDto, PlayerSeasonStateDto } from '@/types';

const TeamViewScreen: React.FC<{ teamId?: string }> = ({ teamId: propTeamId }) => {
  const teams = useGameState((s) => s.leagueTeams);
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [players, setPlayers] = useState<PlayerSeasonStateDto[]>([]);

  const params = new URLSearchParams(window.location.search);
  const seasonId = params.get('season') || '';
  const urlTeamId = propTeamId || window.location.pathname.split('/').pop();

  useEffect(() => {
    const teamObj = teams.find(t => t.id === urlTeamId);
    if (teamObj) setTeam(teamObj);
  }, [teams, urlTeamId]);

  useEffect(() => {
    if (urlTeamId && seasonId) {
      fetch(`/api/team/${urlTeamId}/squad/${seasonId}`)
        .then(r => r.json())
        .then(setPlayers)
        .catch(console.error);
    }
  }, [urlTeamId, seasonId]);

  if (!team) return null;

  return (
    <div className="team-view-overlay">
      <div className="card team-view-card">
        <div className="squad-head team-view-summary">
          <div>
            <h2>Elenco</h2>
            <p>Força {team.rating} • {players.length} jogadores</p>
          </div>
          <button className="ctrl" onClick={() => window.history.back()}>Fechar</button>
        </div>

        <div className="selection-bar team-view-summary">
          <div>
            <b>{team.name} • Elenco completo</b>
            <div className="squad-hint">Acompanhe energia, atributos, gols, cartões e disponibilidade.</div>
          </div>
          <div
            className="team-color-sample"
            style={{
              '--team-primary': team.primaryColor,
              '--team-secondary': team.secondaryColor,
            } as React.CSSProperties}
          />
        </div>

        <div className="squad-table-wrap">
          <table className="squad-table team-view-table">
            <thead>
              <tr>
                <th>Pos</th>
                <th>Jogador</th>
                <th>Idade</th>
                <th>Energia</th>
                <th>Atributos</th>
                <th>Temporada</th>
              </tr>
            </thead>
            <tbody id="teamViewTableBody">
              {players.map(p => (
                <tr key={p.id}>
                  <td>--</td>
                  <td><b>{p.playerId}</b></td>
                  <td>--</td>
                  <td>{p.energy}%</td>
                  <td>--</td>
                  <td>
                    <span>{p.goals || 0} gol{p.goals !== 1 ? 's' : ''}</span>
                    {' '}
                    <span>CA {p.yellowCards || 0}/3</span>
                    {p.suspensionMatches > 0 && <span> 🚫 {p.suspensionMatches}</span>}
                    {p.injury && <span> 🩹</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default TeamViewScreen;
