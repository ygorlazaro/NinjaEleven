import React from 'react';
import type { MatchStateDto } from '@/types';

interface MatchStatsProps {
  state?: MatchStateDto | null;
  full?: boolean;
}

const MatchStats: React.FC<MatchStatsProps> = ({ state, full }) => {
  if (!state) {
    return <div className="stat"><div className="stat-head"><span>Posse</span><span><b>50%</b> — <b>50%</b></span></div></div>;
  }

  const stats = state.stats || [
    { shots: 0, shotsOnTarget: 0, corners: 0, cards: 0, fouls: 0, possession: 50 },
    { shots: 0, shotsOnTarget: 0, corners: 0, cards: 0, fouls: 0, possession: 50 },
  ];

  const home = stats[0];
  const away = stats[1];

  const renderStat = (label: string, homeVal: number, awayVal: number) => (
    <div className="stat">
      <div className="stat-head">
        <span>{label}</span>
        <span><b>{homeVal}</b> — <b>{awayVal}</b></span>
      </div>
      {full && label === 'Posse' && (
        <div className="bars">
          <div className="bar">
            <div style={{ width: `${home.possession}%` }}></div>
          </div>
          <div className="bar right">
            <div style={{ width: `${away.possession}%` }}></div>
          </div>
        </div>
      )}
    </div>
  );

  return (
    <>
      {renderStat('Posse', home.possession, away.possession)}
      {renderStat('Finalizações', home.shots, away.shots)}
      {renderStat('No gol', home.shotsOnTarget, away.shotsOnTarget)}
      {renderStat('Escanteios', home.corners, away.corners)}
      {full && renderStat('Faltas', home.fouls, away.fouls)}
      {full && renderStat('Cartões', home.cards, away.cards)}
      {!full && full === undefined && (
        <div className="bottom-note">
          O motor ainda é propositalmente simples: atributos, formação, energia e aleatoriedade já influenciam os acontecimentos. A lógica será refinada nas próximas versões.
        </div>
      )}
    </>
  );
};

export default MatchStats;
