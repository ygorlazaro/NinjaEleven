import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';

interface SubstitutionModalProps {
  show: boolean;
  team: TeamDto;
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  substitutionsUsed: number;
  /**
   * A player already chosen to come off, because the manager reached him by clicking his
   * card under the scoreboard. He only has to say who replaces him.
   */
  preselectOut?: string | null;
  /**
   * A player who cannot carry on, when the screen was opened because of him rather than
   * because the manager asked. The clock is held for that decision, so there is nowhere to
   * go until somebody is named.
   */
  forcedFor?: string | null;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
  /** Omitted when the match is waiting on the answer: the screen cannot be dismissed. */
  onClose?: () => void;
}

/**
 * Substitutions while the match is being played. The same rules and the same screen as the
 * interval: five substitutions, one goalkeeper on the pitch, and the backend validates the
 * pair again.
 */
const SubstitutionModal: React.FC<SubstitutionModalProps> = ({
  show,
  team,
  lineup,
  bench,
  substitutionsUsed,
  preselectOut = null,
  forcedFor = null,
  busy = false,
  onSubstitute,
  onClose,
}) => {
  if (!show) return null;

  return (
    <div className="modal">
      <div className="modal-card">
        <h2>{forcedFor ? '🩹 Substituição obrigatória' : '🔁 Substituições'}</h2>
        <p style={{ color: 'var(--muted)', fontSize: '12px' }}>{team.name}</p>

        {forcedFor && (
          <p style={{ color: 'var(--danger)', fontSize: '13px', margin: '0 0 8px' }}>
            {forcedFor} não pode continuar. O relógio está parado até alguém entrar no lugar
            dele.
          </p>
        )}

        {/* The key restarts the panel whenever a different player is reached from the team
            sheet, so the pick the manager made there is the pick the panel opens with. */}
        <SubstitutionPanel
          key={preselectOut ?? 'none'}
          lineup={lineup}
          bench={bench}
          used={substitutionsUsed}
          preselectOut={preselectOut}
          busy={busy}
          onSubstitute={onSubstitute}
        />

        {onClose && (
          <div className="modal-actions" style={{ marginTop: '16px' }}>
            <button className="ctrl" onClick={onClose}>Fechar</button>
          </div>
        )}
      </div>
    </div>
  );
};

export default SubstitutionModal;
