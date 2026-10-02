import React from 'react';
import type { MatchOutcome, TeamMatchRecordDto } from '@/types';

export type Form = 'win' | 'draw' | 'loss';

/**
 * What a match meant for the club, read off its two goals.
 *
 * <p>
 * It takes the two numbers and nothing else, so every line in the game that has a scoreline can
 * be asked — a club's own record and the tactics board's form guide are the same question about
 * the same pair of goals, and two screens each carrying their own copy of the comparison are
 * two screens that can disagree about whether a club won.
 * </p>
 */
export const formOf = (record: { goalsFor: number; goalsAgainst: number }): Form => {
  if (record.goalsFor > record.goalsAgainst) return 'win';
  if (record.goalsFor < record.goalsAgainst) return 'loss';
  return 'draw';
};

const FORM_LABEL: Record<Form, string> = { win: 'V', draw: 'E', loss: 'D' };

/** The same three results, said in full, for anyone the colour or the letter does not reach. */
export const FORM_TITLE: Record<Form, string> = {
  win: 'Vitória',
  draw: 'Empate',
  loss: 'Derrota'
};

/**
 * One result as the letter that says it.
 *
 * <para>
 * The letter is drawn by the row's own `form-win` / `form-draw` / `form-loss` class rather than
 * carrying one itself: the rules that colour it are descendant rules, so a badge that coloured
 * itself would be a second set of bands for one letter. Handed a form, it says what the run
 * says and nothing more.
 * </para>
 */
export const FormBadge: React.FC<{ form: Form }> = ({ form }) => (
  <span className="form-result">{FORM_LABEL[form]}</span>
);

interface FormRunProps {
  matches: TeamMatchRecordDto[];
  /** How many of the run to draw. */
  length?: number;
  /** Said when the club has not played, rather than a strip of nothing. */
  emptyLabel?: string;
}

/**
 * A club's last results as a row of letters: the shape of a run read at a glance, before a
 * single opponent name is.
 *
 * The colour says the outcome and the letter says it again, because one of the two reaching
 * a manager is not guaranteed and the other one is a guess. Oldest first, so the run is read
 * in the direction it happened.
 */
const FormRun: React.FC<FormRunProps> = ({ matches, length = 5, emptyLabel = 'sem jogos' }) => {
  // The list arrives newest first, and a run is read in the order it happened.
  const run = matches.slice(0, length).reverse();

  if (run.length === 0) {
    return <span className="form-run__empty">{emptyLabel}</span>;
  }

  return (
    <>
      {run.map((record, index) => {
        const form = formOf(record);

        return (
          <span
            key={`${record.matchId}-${index}`}
            className={`form-run__letter form-${form}`}
            title={`${FORM_TITLE[form]} ${record.goalsFor} x ${record.goalsAgainst} — ${record.opponentName}`}
          >
            {FORM_LABEL[form]}
          </span>
        );
      })}
    </>
  );
};

export default FormRun;

/** How many results a run keeps. It is the number the club page's guide and the table agree on. */
export const FORM_RUN_LENGTH = 5;

const OUTCOME_FORM: Record<MatchOutcome, Form> = {
  Win: 'win',
  Draw: 'draw',
  Loss: 'loss'
};

interface OutcomeFormRunProps {
  outcomes?: MatchOutcome[] | null;
  length?: number;
}

/**
 * The same run, told by the backend instead of worked out from scorelines.
 *
 * The classification table is handed the result of each of the last games as a word, because the
 * table that counts the points is the same one that says which of them were won — and a screen
 * that read the same matches again to reach the same answer would be a second account of them.
 *
 * **A run is never padded with a result.** A club three matchdays in has three, and the places
 * for the other two are drawn as a dash in grey: a coloured letter in an empty place would be a
 * match the season has not played yet, sitting in a table of what it has.
 */
export const OutcomeFormRun: React.FC<OutcomeFormRunProps> = ({
  outcomes,
  length = FORM_RUN_LENGTH
}) => {
  const played = outcomes ?? [];

  return (
    <span className="form-run">
      {Array.from({ length }, (_, index) => {
        const outcome = played[index];

        if (!outcome) {
          return (
            <span key={`empty-${index}`} className="form-run__letter form-empty" title="Ainda não jogado">
              -
            </span>
          );
        }

        const form = OUTCOME_FORM[outcome];

        return (
          <span key={`${outcome}-${index}`} className={`form-run__letter form-${form}`} title={FORM_TITLE[form]}>
            {FORM_LABEL[form]}
          </span>
        );
      })}
    </span>
  );
};
