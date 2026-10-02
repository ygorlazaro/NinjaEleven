import React from 'react';
import { positionLabel } from '@/services/formatters';
import type { MatchPlayerDto } from '@/types';
import HurtBadge from '@/components/Match/HurtBadge';
import { useCountdown } from '@/hooks/useCountdown';

interface PenaltyModalProps {
  show: boolean;
  candidates: MatchPlayerDto[];
  onSelected: (playerId: string) => void;
  onClose: () => void;
  /** Why the last attempt was refused, said where the manager is looking. */
  error?: string | null;
  /**
   * When the window closes, in the backend's clock. It is a moment and not a duration: the
   * fifteen seconds are the engine's, and this only shows what is left of them.
   */
  endsAt?: string | null;
}

const PenaltyModal: React.FC<PenaltyModalProps> = ({ show, candidates, onSelected, onClose, error, endsAt }) => {
  const seconds = useCountdown(endsAt);

  if (!show) return null;

  // The engine already sends the taker list best chance first, and the chance it sends is
  // the very number it rolls. Showing it turns a blind pick into a decision the manager can
  // argue with, which is the whole point of being asked who takes it.
  const best = candidates[0]?.penaltyChance ?? null;

  return (
    <div className="modal" onClick={onClose}>
      <div className="modal-card" onClick={event => event.stopPropagation()}>
        <h2 id="penaltyTitle">⚽ Pênalti!</h2>
        <p id="penaltyDescription" style={{ color: 'var(--muted)' }}>
          Escolha o jogador que vai cobrar. O jogo espera {seconds != null ? `${seconds}s` : 'uns segundos'} pela
          sua decisão — depois disso o motor chama o melhor cobrador do time.
          {best != null && ' A chance é a mesma que o motor vai sortear.'}
        </p>
        {error && (
          <p style={{ color: 'var(--danger)', fontSize: '14px' }}>{error}</p>
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
                <b>
                  {p.name}
                  <HurtBadge player={p} />
                </b>
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
            <span style={{ fontSize: '14px', color: 'var(--muted)' }}>Nenhuma opção disponível</span>
          )}
        </div>
      </div>
    </div>
  );
};

export default PenaltyModal;
