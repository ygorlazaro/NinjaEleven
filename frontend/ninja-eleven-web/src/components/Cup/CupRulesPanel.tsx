import React from 'react';
import type { CupRulesDto } from '@/types';

/**
 * The cup's own rules: how it is drawn, how a tie is decided, and what the winner goes on to play.
 *
 * **This is the gap the bracket and the prize legend leave.** The bracket says which two clubs are
 * in a tie and the legend says what the run is worth, and nothing in between says that a tie is
 * two matches, that the aggregate is what decides it, or that a level aggregate goes straight to
 * penalties. Those three are what a manager planning a run actually needs — the first one decides
 * whether a tie is a bad week or a bad fortnight, the second decides whether losing away from home
 * is fatal, and the third decides whether to expect thirty more minutes or a shootout.
 *
 * **Every number and every sentence on it is the backend's.** `GET /competition/cup-rules` reads
 * them out of `CompetitionRules`, and a screen writing "são jogos de ida e volta" from a constant
 * of its own would be promising a cup the game does not play — and the failure is invisible until
 * the day the constants move, at which point the bracket this page is explaining is still the
 * bracket the game drew. The two would be describing different competitions.
 *
 * **The rounds are listed rather than drawn as a bracket** because a bracket is already on the
 * screen's other tab and this page is not about who is in a tie — it is about how a tie works.
 * What it adds to the bracket is the two numbers the bracket does not carry: how many clubs each
 * round is made of, and which days of the season it is played over.
 */
const CupRulesPanel: React.FC<{ rules: CupRulesDto | null }> = ({ rules }) => {
  if (!rules) {
    return <div className="league-empty">Carregando as regras da copa…</div>;
  }

  return (
    /* Its own grid rather than the league's `league-rules`: that one spans its first card over
       two rows because it holds four panels of very different heights, and a two-panel grid that
       inherited the span would leave an empty cell under the rounds table. Here the wider column
       is the table — five columns of numbers — and the narrower one is three sentences. */
    <div className="cup-rules">
      <section className="league-panel">
        <h3>📐 Como a copa funciona</h3>
        <p className="league-rules__hint">
          São {rules.size} clubes em {rules.tieRounds} fases, e cada fase elimina metade dos
          clubes que chegam nela — até sobrar um campeão.
        </p>

        {/* The two rules a manager plans a tie around, said first because they are the two that
            change what he should do on a night. */}
        <ul className="cup-rules__facts">
          <li>
            <b>Os dois jogos.</b> {rules.aggregateRule}
          </li>
          <li>
            <b>O empate.</b> {rules.levelTieRule}
          </li>
          <li>
            <b>E depois?</b> {rules.winnerTakesRule}
          </li>
        </ul>
      </section>

      <section className="league-panel">
        <h3>🗓️ As fases e os dias</h3>
        <p className="league-rules__hint">
          Cada fase dura dois dias: a primeira perna no dia da fase e a volta no dia seguinte,
          então um clube que joga o campeonato na tarde da ida joga a volta na noite do dia
          seguinte.
        </p>

        <table className="cup-rules__rounds">
          <thead>
            <tr>
              <th>Fase</th>
              <th title="Quantos clubes entram nesta fase">Clubes</th>
              <th title="Quantos confrontos a fase tem, e portanto quantos clubes saem dela">Confrontos</th>
              <th title="O dia da temporada da primeira perna">Ida</th>
              <th title="O dia da temporada da volta">Volta</th>
            </tr>
          </thead>
          <tbody>
            {rules.rounds.map(round => (
              <tr
                key={round.tieRound}
                className={round.tieRound === rules.tieRounds ? 'cup-rules__round--last' : ''}
              >
                <td>{round.name}</td>
                <td>{round.clubsIn}</td>
                <td>{round.ties}</td>
                <td>Dia {round.firstLegDay}</td>
                <td>Dia {round.secondLegDay}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
};

export default CupRulesPanel;