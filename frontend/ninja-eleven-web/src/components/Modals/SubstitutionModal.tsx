import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';

interface SubstitutionModalProps {
  show: boolean;
  team: TeamDto;
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  substitutionsUsed: number;
  /** Which of the club's two shirts this match was played in. */
  kitSide?: 'Home' | 'Away';
  /**
   * A player already chosen to come off, because the manager reached him by clicking his
   * card under the scoreboard. He only has to say who replaces him.
   */
  preselectOut?: string | null;
  /**
   * A player who cannot carry on, when the screen was opened because of him rather than
   * because the manager asked. The match goes on around that decision, so the dialog can be
   * put aside: the engine names somebody from the bench if nobody does.
   */
  forcedFor?: string | null;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
  /** Optional on purpose: a dialog nobody can put aside is a dialog that traps the manager. */
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
  kitSide,
  preselectOut = null,
  forcedFor = null,
  busy = false,
  onSubstitute,
  onClose,
}) => {
  if (!show) return null;

  return (
    <div className="modal" onClick={onClose}>
      <div className="modal-card" onClick={event => event.stopPropagation()}>
        <h2>{forcedFor ? '🩹 Substituição obrigatória' : '🔁 Substituições'}</h2>
        <p style={{ color: 'var(--muted)', fontSize: '14px' }}>{team.name}</p>

        {forcedFor && (
          <p style={{ color: 'var(--danger)', fontSize: '15px', margin: '0 0 8px' }}>
            {forcedFor} não pode continuar. O jogo continua — se ninguém escolher, o motor
            traz o melhor do banco.
          </p>
        )}

        {/* The key restarts the panel whenever a different player is reached from the team
            sheet, so the pick the manager made there is the pick the panel opens with. */}
        <SubstitutionPanel
          key={preselectOut ?? 'none'}
          team={team}
          kitSide={kitSide}
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
