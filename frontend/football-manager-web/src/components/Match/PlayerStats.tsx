import React from 'react';
import type { MatchPlayerDto } from '@/types';
import { positionLabel, attrLine, energyClass, energyPercent } from '@/services/formatters';

interface PlayerStatsProps {
  homeLineup: MatchPlayerDto[];
  awayLineup: MatchPlayerDto[];
}

const PlayerStats: React.FC<PlayerStatsProps> = ({ homeLineup, awayLineup }) => {
  const renderTeamStats = (players: MatchPlayerDto[], label: string) => (
    <div className="player-stat-team">
      <div className="player-stat-team-title">{label}</div>
      <table className="player-stats-table">
        <thead>
          <tr>
            <th>Pos</th>
            <th>Jogador</th>
            <th>Energia</th>
            <th>Gols</th>
            <th>Cartões</th>
            <th>Defesas</th>
            <th>Finalizações</th>
          </tr>
        </thead>
        <tbody>
          {players.map(p => {
            const stats = p.matchStats || { goals: 0, cards: 0, saves: 0, shots: 0, shotOnTarget: 0 };
            const isSentOff = p.redCard;
            return (
              <tr key={p.playerId} className={isSentOff ? 'sent-off' : ''}>
                <td>
                  <span className="mini-pos">{positionLabel(p.position)}</span>
                </td>
                <td className="player-name-cell">
                  <b>{p.name}</b>
                  {p.redCard && <span style={{ color: 'var(--red)' }}> 🟥</span>}
                  {p.matchStats?.cardMinutes && p.matchStats.cardMinutes.length > 0 && (
                    <span className="subtle"> {p.matchStats.cardMinutes.map(m => `${m}'`).join(', ')}</span>
                  )}
                </td>
                <td>
                  <div className={`pc-energy-fill ${energyClass(p.energy)}`} style={{ width: energyPercent(p.energy) }}></div>
                </td>
                <td>
                  <b>{p.matchStats?.goals || 0}</b>
                  {p.matchStats?.goalMinutes && p.matchStats.goalMinutes.length > 0 && (
                    <span className="subtle"> {p.matchStats.goalMinutes.map(m => `${m}'`).join(', ')}</span>
                  )}
                </td>
                <td>{p.matchStats?.cards || 0}</td>
                <td>{p.matchStats?.saves || 0}</td>
                <td>{p.matchStats?.shots || 0} ({p.matchStats?.shotsOnTarget || 0})</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );

  return (
    <div className="player-stats-list">
      {renderTeamStats(homeLineup, 'Sua equipe')}
      {renderTeamStats(awayLineup, 'Adversário')}
    </div>
  );
};

export default PlayerStats;
