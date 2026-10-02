import React from 'react';
import type { MatchStateDto } from '@/types';

interface MatchStatsProps {
  state?: MatchStateDto | null;
}

/**
 * The one statistics view. It used to be two tabs reading the same numbers, one of them
 * hiding the fouls and the cards, so there is a single list here with every row the match
 * produces and a bar wherever there is a share to see.
 *
 * **Nothing here is drawn before the match has said it.** The panel used to fall back on a
 * table of zeros with a 50/50 possession, and that fallback is the one thing a statistics
 * table must never be: a match in which nothing happened legitimately *is* seven rows of
 * zeros, so a reader cannot tell an unstarted panel from a goalless first half — and the
 * number that decides the panel is the one number that would be wrong. It says the numbers
 * are coming instead.
 */
const MatchStats: React.FC<MatchStatsProps> = ({ state }) => {
  const stats = state?.stats;

  if (!stats || stats.length < 2) {
    return <p className="league-empty">As estatísticas aparecem assim que a bola rolar.</p>;
  }

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
