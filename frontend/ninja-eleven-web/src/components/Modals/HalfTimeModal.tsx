import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';
import { useCountdown } from '@/hooks/useCountdown';

interface HalfTimeModalProps {
  show: boolean;
  homeTeam: TeamDto;
  awayTeam: TeamDto;
  score: string;
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  substitutionsUsed: number;
  /** Which of the club's two shirts this match was played in. */
  kitSide?: 'Home' | 'Away';
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
  onContinue: () => void;
  /**
   * Puts the dialog aside without ending the break. The second half begins when the backend
   * says so either way, so closing it is only a manager looking at the pitch again.
   */
  onClose?: () => void;
  /**
   * When the break closes, in the backend's clock: the twenty seconds that are the engine's,
   * drawn here rather than counted by the screen.
   */
  endsAt?: string | null;
}

const HalfTimeModal: React.FC<HalfTimeModalProps> = ({
  show,
  homeTeam,
  awayTeam,
  score,
  lineup,
  bench,
  substitutionsUsed,
  kitSide,
  busy = false,
  onSubstitute,
  onContinue,
  onClose,
  endsAt,
}) => {
  const seconds = useCountdown(endsAt);

  if (!show) return null;

  return (
    <div className="modal" onClick={onClose}>
      <div className="modal-card halftime-card" onClick={event => event.stopPropagation()}>
        <h2>⏸ Intervalo</h2>
        <div className="modal-score" id="halfScore">{score}</div>
        <p id="breakDescription" style={{ color: 'var(--muted)' }}>
          {homeTeam.name} {score} {awayTeam.name}. Revise o time e faça as substituições
          antes de começar o segundo tempo.
        </p>
        {seconds != null && (
          <p className="halftime-countdown" role="status">
            O segundo tempo começa em <b>{seconds}s</b>, continue ou não — o relógio do servidor é quem conta.
          </p>
        )}

        <SubstitutionPanel
          team={homeTeam}
          kitSide={kitSide}
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
