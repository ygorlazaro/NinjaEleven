import React from 'react';
import type { PlayerDto } from '@/types';

interface PenaltyModalProps {
  show: boolean;
  candidates: PlayerDto[];
  onSelected: (playerId: string) => void;
  onClose: () => void;
}

const PenaltyModal: React.FC<PenaltyModalProps> = ({ show, candidates, onSelected, onClose }) => {
  if (!show) return null;

  return (
    <div className="modal">
      <div className="modal-card">
        <h2 id="penaltyTitle">⚽ Pênalti!</h2>
        <p id="penaltyDescription" style={{ color: 'var(--muted)' }}>
          Escolha o jogador que vai cobrar.
        </p>
        <div id="penaltyOptions" className="penalty-options">
          {candidates.length > 0 ? (
            candidates.map(p => (
              <div
                key={p.id}
                className="penalty-player"
                onClick={() => onSelected(p.id)}
                style={{ '--team-primary': '#f2d34f', '--team-secondary': '#f2d34f' } as React.CSSProperties}
              >
                <b>{p.name}</b>
                <span>{p.position} • Vel {p.speed} Des {p.accuracy}</span>
              </div>
            ))
          ) : (
            <span style={{ fontSize: '12px', color: 'var(--muted)' }}>Nenhuma opção disponível</span>
          )}
        </div>
      </div>
    </div>
  );
};

export default PenaltyModal;
