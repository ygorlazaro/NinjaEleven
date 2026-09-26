import React from 'react';
import type { TeamDto } from '@/types';

interface HalfTimeModalProps {
  show: boolean;
  homeTeam: TeamDto;
  awayTeam: TeamDto;
  score: string;
  onContinue: () => void;
}

const HalfTimeModal: React.FC<HalfTimeModalProps> = ({ show, homeTeam, awayTeam, score, onContinue }) => {
  if (!show) return null;

  return (
    <div className="modal">
      <div className="modal-card halftime-card">
        <h2>⏸ Intervalo</h2>
        <div className="modal-score" id="halfScore">{score}</div>
        <p id="breakDescription" style={{ color: 'var(--muted)' }}>
          Você pode revisar as estatísticas e fazer substituições antes do segundo tempo.
        </p>
        <div id="halfSummary" style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '16px' }}>
          Placar no intervalo: {score}
        </div>
        <div className="half-sub-area">
          <b>Substituições no intervalo</b>
          <div className="half-sub-help">
            Clique em um titular e em um reserva da mesma posição. Jogadores expulsos não podem ser selecionados.
          </div>
          <div id="halfPlayers" style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
            <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Funcionalidade em desenvolvimento</span>
          </div>
          <div id="halfSubControls" className="sub-controls">
            <button className="ctrl" style={{ width: '100%' }}>Automaticamente</button>
          </div>
        </div>
        <button className="primary" style={{ marginTop: '18px', width: '100%' }} onClick={onContinue}>
          Começar segundo tempo
        </button>
      </div>
    </div>
  );
};

export default HalfTimeModal;
