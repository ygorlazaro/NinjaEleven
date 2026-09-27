import React from 'react';
import type { MatchContextDto } from '@/types';
import { ClubName } from '@/components/Common/Names';

/**
 * Where the match is, said above the score.
 *
 * It used to be a fixed line — "DIVISÃO 3 • JOGO-TREINO" — above the score of every match
 * in the pyramid, which is a header that cannot be wrong about a match because it says
 * nothing about it. A manager reads this line before anything else: a Supercup is one match
 * in a season, a cup return leg is decided by an aggregate, and a championship matchday is
 * eighteen games played at the same time.
 *
 * Everything here is read from the backend's context. A screen that worked the phase out
 * from a round number would have to know that a cup window's number is not a round number,
 * and a screen that gets that wrong says "quartas de final" over a round-of-16 leg.
 */
const MatchContextBar: React.FC<{ context: MatchContextDto | null }> = ({ context }) => {
  if (!context) {
    return <div className="competition">CARREGANDO...</div>;
  }

  const phase = context.legLabel
    ? `${context.phaseName} • ${context.legLabel}`
    : context.phaseName;

  return (
    <>
      <div className="competition">
        <span>
          {context.seasonName} • DIA {context.matchDayNumber} • {context.editionName.toUpperCase()} •{' '}
          {phase.toUpperCase()}
        </span>
      </div>

      <div className="match-ground">
        <span className="match-ground__icon" aria-hidden="true">
          ▣
        </span>
        <span className="match-ground__name">{context.stadiumName || 'Estádio a definir'}</span>
        {context.stadiumCapacity > 0 && (
          <span className="match-ground__capacity">
            {context.stadiumCapacity.toLocaleString('pt-BR')} lugares
          </span>
        )}
      </div>
    </>
  );
};

/**
 * The leg before this one, under the score.
 *
 * It is the same size as nothing else on the screen on purpose: it is context, not the
 * result. What makes it necessary is that "1 × 0" means something entirely different in a
 * tie whose aggregate is already 2 × 1, and a return leg the manager cannot place in the tie
 * is a match he is watching without knowing what it is for.
 */
const FirstLegStrip: React.FC<{ context: MatchContextDto | null }> = ({ context }) => {
  const leg = context?.firstLeg;

  if (!context || !leg) {
    return null;
  }

  return (
    <div className="first-leg" id="firstLeg">
      <span className="first-leg__label">1ª perna</span>
      <ClubName teamId={leg.homeTeamId}>{leg.homeTeamName}</ClubName>
      <b>
        {leg.homeGoals} × {leg.awayGoals}
      </b>
      <ClubName teamId={leg.awayTeamId}>{leg.awayTeamName}</ClubName>
    </div>
  );
};

export { MatchContextBar, FirstLegStrip };
export default MatchContextBar;
