import React from 'react';
import type { MatchLineupDto } from '@/types';
import { positionLabel, attrLine, energyClass, energyPercent } from '@/services/formatters';

interface LineupViewProps {
  lineup: MatchLineupDto;
}

const LineupView: React.FC<LineupViewProps> = ({ lineup }) => {
  const renderPlayerCard = (p: any, isBench: boolean = false) => {
    const isGK = p.position === 'GK';
    const energyPct = energyPercent(p.energy);

    return (
      <div
        key={p.playerId}
        className={`player-card ${isBench ? 'bench-card' : ''}`}
        style={{
          '--team-primary': lineup.homeTeam.primaryColor,
          '--team-secondary': lineup.homeTeam.secondaryColor,
        } as React.CSSProperties}
      >
        <div className="player-top">
          <span className="player-pos">{positionLabel(p.position)}</span>
          <span className="player-name">{p.name.split(' ')[0]}</span>
          <span className={`player-energy ${energyClass(p.energy)}`}>{Math.round(p.energy)}%</span>
        </div>
        <div className="player-status">
          {energyPct}
        </div>
        <div className="player-stats">
          {p.matchStats && (
            <span className="attr-chip">⚽ {p.matchStats.goals}</span>
          )}
          {p.matchStats && p.matchStats.cards > 0 && (
            <span className="attr-chip">🟨 {p.matchStats.cards}</span>
          )}
        </div>
      </div>
    );
  };

  const userTeamIdx = lineup.userTeamIndex;

  return (
    <div className="lineup">
      <div className="half-sub-area">
        <div className="half-sub-help">Time visitante — {lineup.awayTeam.name}</div>
        <div className="bench-grid" id="benchList">
          {lineup.awayBench.map(p => renderPlayerCard(p, true))}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Seu time — {lineup.homeTeam.name}</div>
        <div className="player-grid" id="lineupList">
          {lineup.homeLineup.map(p => renderPlayerCard(p))}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Banco — {lineup.homeTeam.name}</div>
        <div className="bench-grid" id="benchList">
          {lineup.homeBench.map(p => renderPlayerCard(p, true))}
        </div>
      </div>
    </div>
  );
};

export default LineupView;
