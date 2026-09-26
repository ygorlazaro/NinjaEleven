import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';

interface HalfTimeModalProps {
  show: boolean;
  homeTeam: TeamDto;
  awayTeam: TeamDto;
  score: string;
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  substitutionsUsed: number;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
  onContinue: () => void;
}

const HalfTimeModal: React.FC<HalfTimeModalProps> = ({
  show,
  homeTeam,
  awayTeam,
  score,
  lineup,
  bench,
  substitutionsUsed,
  busy = false,
  onSubstitute,
  onContinue,
}) => {
  if (!show) return null;

  return (
    <div className="modal">
      <div className="modal-card halftime-card">
        <h2>⏸ Intervalo</h2>
        <div className="modal-score" id="halfScore">{score}</div>
        <p id="breakDescription" style={{ color: 'var(--muted)' }}>
          {homeTeam.name} {score} {awayTeam.name}. Revise o time e faça as substituições
          antes de começar o segundo tempo.
        </p>

        <SubstitutionPanel
          lineup={lineup}
          bench={bench}
          used={substitutionsUsed}
          busy={busy}
          onSubstitute={onSubstitute}
        />

        <button className="primary" style={{ marginTop: '18px', width: '100%' }} onClick={onContinue}>
          Começar segundo tempo
        </button>
      </div>
    </div>
  );
};

export default HalfTimeModal;
