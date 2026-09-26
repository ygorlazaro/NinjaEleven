import React from 'react';
import type { ScorerDto } from '@/types';

interface ScorersListProps {
  scorers: ScorerDto[];
}

const ScorersList: React.FC<ScorersListProps> = ({ scorers }) => {
  if (scorers.length === 0) {
    return <div className="league-empty">Ainda não há gols no campeonato.</div>;
  }

  return (
    <table className="scorers">
      <thead>
        <tr>
          <th>#</th>
          <th>Jogador</th>
          <th>Idade</th>
          <th>Gols</th>
          <th>Time</th>
        </tr>
      </thead>
      <tbody>
        {scorers.map((s, i) => (
          <tr key={s.playerId}>
            <td>{i + 1}</td>
            <td><b>{s.playerName}</b></td>
            <td>{s.age}</td>
            <td><b>{s.goals}</b></td>
            <td>{s.teamName}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
};

export default ScorersList;
