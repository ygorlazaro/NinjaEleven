import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';

interface SubstitutionModalProps {
  show: boolean;
  team: TeamDto;
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  substitutionsUsed: number;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
  onClose: () => void;
}

/**
 * Substitutions while the match is being played. The same rules as the interval: five
 * substitutions, one goalkeeper on the pitch, and the backend validates the pair again.
 */
const SubstitutionModal: React.FC<SubstitutionModalProps> = ({
  show,
  team,
  lineup,
  bench,
  substitutionsUsed,
  busy = false,
  onSubstitute,
  onClose,
}) => {
  if (!show) return null;

  return (
    <div className="modal">
      <div className="modal-card">
        <h2>🔁 Substituições</h2>
        <p style={{ color: 'var(--muted)', fontSize: '12px' }}>{team.name}</p>

        <SubstitutionPanel
          lineup={lineup}
          bench={bench}
          used={substitutionsUsed}
          busy={busy}
          onSubstitute={onSubstitute}
        />

        <div className="modal-actions" style={{ marginTop: '16px' }}>
          <button className="ctrl" onClick={onClose}>Fechar</button>
        </div>
      </div>
    </div>
  );
};

export default SubstitutionModal;
