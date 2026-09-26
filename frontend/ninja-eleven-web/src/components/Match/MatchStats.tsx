import React from 'react';
import type { MatchStateDto } from '@/types';

interface MatchStatsProps {
  state?: MatchStateDto | null;
}

/**
 * The one statistics view. It used to be two tabs reading the same numbers, one of them
 * hiding the fouls and the cards, so there is a single list here with every row the match
 * produces and a bar wherever there is a share to see.
 */
const MatchStats: React.FC<MatchStatsProps> = ({ state }) => {
  if (!state) {
    return <div className="stat"><div className="stat-head"><span>Posse</span><span><b>50%</b> — <b>50%</b></span></div></div>;
  }

  const stats = state.stats || [
    { shots: 0, shotsOnTarget: 0, corners: 0, cards: 0, fouls: 0, possession: 50, saves: 0 },
    { shots: 0, shotsOnTarget: 0, corners: 0, cards: 0, fouls: 0, possession: 50, saves: 0 },
  ];

  const home = stats[0];
  const away = stats[1];

  const row = (label: string, homeVal: number, awayVal: number, share?: { home: number; away: number }) => (
    <div className="stat">
      <div className="stat-head">
        <span>{label}</span>
        <span><b>{homeVal}</b> — <b>{awayVal}</b></span>
      </div>
      {share && (
        <div className="bars">
          <div className="bar">
            <div style={{ width: `${share.home}%` }}></div>
          </div>
          <div className="bar right">
            <div style={{ width: `${share.away}%` }}></div>
          </div>
        </div>
      )}
    </div>
  );

  return (
    <>
      {row('Posse', home.possession, away.possession, { home: home.possession, away: away.possession })}
      {row('Finalizações', home.shots, away.shots)}
      {row('No gol', home.shotsOnTarget, away.shotsOnTarget)}
      {row('Defesas', home.saves, away.saves)}
      {row('Escanteios', home.corners, away.corners)}
      {row('Faltas', home.fouls, away.fouls)}
      {row('Cartões', home.cards, away.cards)}
    </>
  );
};

export default MatchStats;
