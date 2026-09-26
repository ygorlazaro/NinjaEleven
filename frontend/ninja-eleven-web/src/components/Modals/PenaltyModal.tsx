import React from 'react';
import { positionLabel } from '@/services/formatters';
import type { MatchPlayerDto } from '@/types';

interface PenaltyModalProps {
  show: boolean;
  candidates: MatchPlayerDto[];
  onSelected: (playerId: string) => void;
  onClose: () => void;
  /** Why the last attempt was refused, said where the manager is looking. */
  error?: string | null;
}

const PenaltyModal: React.FC<PenaltyModalProps> = ({ show, candidates, onSelected, onClose, error }) => {
  if (!show) return null;

  // The engine already sends the taker list best chance first, and the chance it sends is
  // the very number it rolls. Showing it turns a blind pick into a decision the manager can
  // argue with, which is the whole point of being asked who takes it.
  const best = candidates[0]?.penaltyChance ?? null;

  return (
    <div className="modal">
      <div className="modal-card">
        <h2 id="penaltyTitle">⚽ Pênalti!</h2>
        <p id="penaltyDescription" style={{ color: 'var(--muted)' }}>
          Escolha o jogador que vai cobrar. O relógio espera a sua decisão.
          {best != null && ' A chance é a mesma que o motor vai sortear.'}
        </p>
        {error && (
          <p style={{ color: 'var(--danger)', fontSize: '12px' }}>{error}</p>
        )}

        <div id="penaltyOptions" className="penalty-options">
          {candidates.length > 0 ? (
            candidates.map(p => (
              <div
                key={p.playerId}
                className="penalty-player"
                onClick={() => onSelected(p.playerId)}
                style={{ '--team-primary': '#f2d34f', '--team-secondary': '#f2d34f' } as React.CSSProperties}
              >
                <b>{p.name}</b>
                <span>
                  {positionLabel(p.position)} • Fin {p.accuracy} • Dri {p.dribbling} • For{' '}
                  {p.strength}
                </span>
                {p.penaltyChance != null && (
                  <span className="penalty-chance">{Math.round(p.penaltyChance * 100)}% de chance</span>
                )}
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
