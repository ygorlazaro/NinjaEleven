import React from 'react';
import type { MatchLineupDto } from '@/types';

interface TacticsViewProps {
  homeLineup: any[];
  awayLineup: any[];
}

const TacticsView: React.FC<TacticsViewProps> = ({ homeLineup, awayLineup }) => {
  const countPositions = (players: any[]) => {
    const counts: Record<string, number> = { GK: 0, DEF: 0, MID: 0, ATT: 0 };
    players.forEach(p => {
      counts[p.position] = (counts[p.position] || 0) + 1;
    });
    return counts;
  };

  const homeCounts = countPositions(homeLineup);
  const awayCounts = countPositions(awayLineup);

  const formation = (counts: Record<string, number>): string => {
    return `${counts.DEF}-${counts.MID}-${counts.ATT}`;
  };

  return (
    <div>
      <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '8px' }}>
        Sua formação
      </div>
      <div id="formationGrid" className="form-grid">
        <div className={`formation ${formation(homeCounts) === formation(homeCounts) ? 'selected' : ''}`}>
          {formation(homeCounts)}
        </div>
      </div>

      <div className="pitch" id="pitch">
        <div style={{ padding: '10px', color: 'var(--muted)', fontSize: '11px' }}>
          Formação do seu time: {formation(homeCounts)}
        </div>
      </div>

      <div className="bottom-note">
        A tática é criada automaticamente pela composição dos 11 jogadores.
        Ao substituir, a formação é recalculada (ex.: 4-4-2 → 4-3-3).
      </div>
    </div>
  );
};

export default TacticsView;
