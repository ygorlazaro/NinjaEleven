import React from 'react';

/**
 * The three things that keep a man off the pitch, drawn beside his name.
 *
 * <p>
 * A suspension, a knock and a declared retirement are read on the squad table, on the base
 * screen and on the tactics board, and they used to be drawn three times over with three
 * different tooltips. They are one rule — <c>IsAvailable</c> in the domain decides who can
 * play — so they are drawn here once and the screens say what they have.
 * </p>
 *
 * <p>
 * Only the mark is the screen's; the numbers are the backend's, and a mark with no number in
 * its tooltip is a mark that has cost a manager a trip to the club's own page to read.
 * </p>
 */
const PlayerStatusMarks: React.FC<{
  /** The injury he is carrying, as the domain spells it — `None` when he is fit. */
  injury?: string | null;
  /** Matches of his club the injury still keeps him out of. */
  injuryMatchesRemaining?: number;
  /** Matches of his club the suspension still keeps him out of. */
  suspensionMatches?: number;
  /** Whether he has announced this is his last season. */
  retiring?: boolean;
}> = ({ injury, injuryMatchesRemaining, suspensionMatches, retiring }) => (
  <>
    {retiring && (
      <span className="retiring-mark" title="Aposentadoria declarada">🏁</span>
    )}
    {injury && injury !== 'None' && (
      <span
        className="injury-mark"
        title={
          injuryMatchesRemaining && injuryMatchesRemaining > 0
            ? `Lesionado: ${injury} • ${injuryMatchesRemaining} jogo(s) de retorno`
            : `Lesionado: ${injury}`
        }
      >
        🩹
      </span>
    )}
    {suspensionMatches !== undefined && suspensionMatches > 0 && (
      <span className="suspension-mark" title={`Suspenso: ${suspensionMatches} partida(s)`}>🟥</span>
    )}
  </>
);

export default PlayerStatusMarks;