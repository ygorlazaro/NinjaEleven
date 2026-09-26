import React from 'react';
import type { MatchLineupDto, MatchPlayerDto } from '@/types';
import { positionLabel, energyClass, energyPercent, sortByPosition } from '@/services/formatters';

interface LineupViewProps {
  lineup: MatchLineupDto;
}

/**
 * The pitch and the bench, in the order a manager reads a team sheet: goalkeepers,
 * defenders, midfielders, attackers, and by name inside each group. A player who left
 * the pitch during the match is shown as he is, because his club has to play the rest of
 * the game without him.
 */
const LineupView: React.FC<LineupViewProps> = ({ lineup }) => {
  const renderPlayerCard = (p: MatchPlayerDto, isBench: boolean = false) => {
    const energyPct = energyPercent(p.energy);
    const isKeeper = p.position === 'GK' || p.emergencyGK;
    const teamColors = {
      '--team-primary': lineup.homeTeam.primaryColor,
      '--team-secondary': lineup.homeTeam.secondaryColor,
    } as React.CSSProperties;

    return (
      <div
        key={p.playerId}
        className={`player-card ${isBench ? 'bench-card' : ''} ${p.redCard ? 'sent-off' : ''} ${
          p.injuredOff ? 'injured' : ''
        }`}
        style={teamColors}
      >
        <div className="player-top">
          <span className="player-pos">
            {p.emergencyGK ? 'GOL*' : positionLabel(p.position)}
          </span>
          <span className="player-name">{p.name}</span>
          <span className={`player-energy ${energyClass(p.energy)}`}>{Math.round(p.energy)}%</span>
        </div>
        <div className="player-status">{energyPct}</div>
        <div className="player-stats">
          {p.goals > 0 && <span className="attr-chip">⚽ {p.goals}</span>}
          {p.matchYellowCards > 0 && <span className="attr-chip">🟨 {p.matchYellowCards}</span>}
          {p.redCard && <span className="attr-chip">🟥</span>}
          {p.injuredOff && <span className="attr-chip">🚑</span>}
          {p.emergencyGK && <span className="attr-chip" title="Assumiu a meta sem goleiro">🧤</span>}
        </div>
      </div>
    );
  };

  const opponentBench = sortByPosition(lineup.awayBench);
  const homeLineup = sortByPosition(lineup.homeLineup);
  const homeBench = sortByPosition(lineup.homeBench);

  return (
    <div className="lineup">
      <div className="half-sub-area">
        <div className="half-sub-help">Banco — {lineup.awayTeam.name}</div>
        <div className="bench-grid">
          {opponentBench.map(p => renderPlayerCard(p, true))}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Em campo — {lineup.homeTeam.name}</div>
        <div className="player-grid">
          {homeLineup.map(p => renderPlayerCard(p))}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Banco — {lineup.homeTeam.name}</div>
        <div className="bench-grid">
          {homeBench.map(p => renderPlayerCard(p, true))}
        </div>
      </div>
    </div>
  );
};

export default LineupView;
