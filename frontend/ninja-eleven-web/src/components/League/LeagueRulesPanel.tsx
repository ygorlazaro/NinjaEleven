import React from 'react';
import type {
  DivisionBandDto,
  DivisionPurseDto,
  DivisionRuleDto,
  PyramidRulesDto,
  TopScorerPrizeListDto
} from '@/types';
import { formatLimo } from '@/services/limo';
import DivisionTrophy from '@/components/League/DivisionTrophy';
import PrizeLegend from '@/components/League/PrizeLegend';
import TopScorerPrizePanel from '@/components/League/TopScorerPrizePanel';

interface LeagueRulesPanelProps {
  /** The pyramid's own rules, or null while the backend has not answered. */
  rules: PyramidRulesDto | null;
  /** Every division's purse, and what each position in it is worth. */
  purses: DivisionPurseDto[];
  /** This edition's artilharia prize, the three shares and who holds them. */
  scorerPrize: TopScorerPrizeListDto | null;
  /** The tier of the division the table above is showing. */
  activeTier?: number | null;
}

/**
 * The rules of the page a manager is looking at: what a place in the table is worth, what it
 * moves, and the order two clubs level on everything else are settled in.
 *
 * **Every number on it is the backend's.** The bands are the movement the close of the season
 * makes, the chain is the chain the sort walks, and the shares are the shares the last club's
 * rounding remainder adds up to. A "regras" page that wrote "os 4 primeiros sobem" out of a
 * constant of its own would be promising a season the game does not play — the first division
 * has nowhere to promote to and the last nowhere to be relegated from — so the words and the
 * numbers both arrive from `/league/rules` and there is nothing here to keep in step.
 *
 * **The four divisions are all here, not only the one on the table above.** The money is a
 * ladder and the movement is a ladder, and a manager reading his ninth place wants to know what
 * the ninth place of the division above is worth and where the clubs under him are going. The
 * division the dropdown is on is marked, not singled out.
 *
 * **The prize tables are the ones the season pays.** They are the same components the cup's own
 * legend draws, because the money is one thing said in two places rather than two things — and
 * a "regras" page that restated them would be a second set of figures that a division's purse
 * changed without reaching.
 */
const LeagueRulesPanel: React.FC<LeagueRulesPanelProps> = ({
  rules,
  purses,
  scorerPrize,
  activeTier
}) => {
  if (!rules) {
    return <div className="league-empty">Carregando as regras do campeonato…</div>;
  }

  const wholeTable = rules.tieBreakers.filter(criterion => criterion.wholeTable);
  const tiedClubs = rules.tieBreakers.filter(criterion => !criterion.wholeTable);

  return (
    <div className="league-rules">
      <section className="league-panel">
        <h3>📐 Regras de cada divisão</h3>
        <p className="league-rules__hint">
          São {rules.divisionCount} divisões com {rules.clubsPerDivision} clubes cada. Uma
          divisão termina a temporada com os mesmos {rules.clubsPerDivision} clubes que começou:
          quem sai de uma entra da outra, e a última não tem divisão abaixo dela.
        </p>

        <div className="league-rules__divisions">
          {rules.divisions.map(division => (
            <DivisionRules
              key={division.tier}
              division={division}
              active={division.tier === activeTier}
            />
          ))}
        </div>
      </section>

      <section className="league-panel">
        <h3>⚖️ Critérios de desempate</h3>
        <p className="league-rules__hint">
          A tabela é ordenada por esta cadeia, nessa ordem, e o primeiro critério que separa dois
          clubes é o que fica na frente. É a mesma cadeia que o backend usa para ordenar a
          tabela e para decidir a posição de acesso e de rebaixamento.
        </p>

        <ChainList title="Valem para a tabela inteira" criteria={wholeTable} />
        <ChainList title="Só entre os clubes ainda empatados" criteria={tiedClubs} />
      </section>

      <section className="league-panel">
        <h3>💰 Premiação do campeonato</h3>
        <PrizeLegend purses={purses} tier={activeTier} />
      </section>

      <section className="league-panel">
        <TopScorerPrizePanel prize={scorerPrize} />
      </section>
    </div>
  );
};

/**
 * One division's rules: the bands of its table and what finishing in each of them is worth.
 *
 * **The bands overlap where the rules overlap.** First place of the second division is both the
 * champion of it and one of the four that go up, and saying it twice is the truth rather than a
 * repeated row — the title is a trophy and the biggest share of the purse, and the access is a
 * place in the division above, and a club in that position gets both.
 */
const DivisionRules: React.FC<{ division: DivisionRuleDto; active: boolean }> = ({ division, active }) => (
  <article className={`division-rule ${active ? 'division-rule--active' : ''}`}>
    <header className="division-rule__head">
      <DivisionTrophy tier={division.tier} size={20} />
      <b className="division-rule__name">{division.name}</b>
      <span className="division-rule__purse">{formatLimo(division.purse)}</span>
    </header>

    <div className="division-rule__bands">
      {division.bands.map(band => (
        <div
          key={`${band.kind}-${band.fromPosition}`}
          className={`division-rule__band division-rule__band--${band.kind.toLowerCase()}`}
        >
          <span className="division-rule__positions">{placeRange(band)}</span>
          <span className="division-rule__what">
            <b>{band.label}</b>
            <span className="division-rule__meaning">{band.meaning}</span>
          </span>
        </div>
      ))}
    </div>
  </article>
);

/**
 * One end of the tiebreaker chain, in the order it is applied.
 *
 * The two ends are drawn as two lists rather than one list with a flag on each, because they are
 * two different questions: the first decides the table and the second only breaks a tie the
 * first could not. The number stays on the row so the order across the two lists is readable —
 * a manager reading the fourth line needs to know there are three above it and not four.
 */
const ChainList: React.FC<{
  title: string;
  criteria: { order: number; label: string; detail: string }[];
}> = ({ title, criteria }) => {
  if (criteria.length === 0) return null;

  return (
    <>
      <h4 className="league-rules__chain-title">{title}</h4>
      <ol className="chain">
        {criteria.map(criterion => (
          <li key={criterion.order} className="chain__item">
            <span className="chain__order">{criterion.order}</span>
            <span className="chain__what">
              <b>{criterion.label}</b>
              <span className="chain__detail">{criterion.detail}</span>
            </span>
          </li>
        ))}
      </ol>
    </>
  );
};

/**
 * A band as a range of places: "1º lugar" for one place and "5º ao 12º" for the rest.
 *
 * The numbers come from the band rather than from a legend written here, so a pyramid that
 * promotes a different number of clubs says a different thing on this page than it did last
 * season.
 */
const placeRange = (band: DivisionBandDto): string => {
  if (band.fromPosition === band.toPosition) return `${band.fromPosition}º lugar`;
  return `${band.fromPosition}º ao ${band.toPosition}º`;
};

export default LeagueRulesPanel;