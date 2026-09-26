import React from 'react';
import type { MatchResult } from '@/types';

interface EndModalProps {
  show: boolean;
  result: MatchResult | null;
  onClose: () => void;
}

const EndModal: React.FC<EndModalProps> = ({ show, result, onClose }) => {
  if (!show) return null;

  if (!result) {
    return (
      <div className="modal">
        <div className="modal-card">
          <h2>Fim de jogo</h2>
          <button className="ctrl" onClick={onClose}>Fechar</button>
        </div>
      </div>
    );
  }

  const endStats = [
    { label: 'Finalizações', home: result.homeShots, away: result.awayShots },
    { label: 'No gol', home: result.homeShotsOnTarget, away: result.awayShotsOnTarget },
    { label: 'Escanteios', home: result.homeCorners, away: result.awayCorners },
  ];

  return (
    <div className="modal">
      <div className="modal-card">
        <h2>Fim de jogo</h2>
        <div className="modal-score" id="finalScore">{result.homeScore} × {result.awayScore}</div>

        <div className="end-stats" id="endStats">
          {endStats.map(s => (
            <div className="end-stat" key={s.label}>
              <span>{s.label}</span>
              <div style={{ display: 'flex', justifyContent: 'center', gap: '8px' }}>
                <b>{s.home}</b>
                <b>×</b>
                <b>{s.away}</b>
              </div>
            </div>
          ))}
        </div>

        <div className="modal-actions" style={{ marginTop: '18px' }}>
          <button className="ctrl" onClick={onClose}>Fechar</button>
          <button className="primary" onClick={() => window.location.hash = '#/league'}>Voltar ao campeonato</button>
        </div>
      </div>
    </div>
  );
};

export default EndModal;
