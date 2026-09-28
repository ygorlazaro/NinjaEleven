import React from 'react';
import type { TopScorerPrizeListDto } from '@/types';
import { formatLimo } from '@/services/limo';
import CupTrophy from '@/components/Cup/CupTrophy';
import DivisionTrophy from '@/components/League/DivisionTrophy';
import TopScorerPrizeRows, { formatRate, shareLabel } from '@/components/League/TopScorerPrizeRows';

/**
 * What a competition's artilharia is paid, and who is holding the three places.
 *
 * **It is a share of the champion's own prize, and the panel says the share as well as the
 * money.** Ten per cent of a title is a rule a manager can plan a season around and a figure
 * he cannot: a striker's wage is a number, and a striker's wage plus a tenth of a title is a
 * reason to sign him. So the base is printed, the three shares are printed, and the amount each
 * place is worth today is printed underneath them.
 *
 * **A competition's artilharia is a share of that competition's own champion's prize.** The
 * cup's three men are therefore all paid out of the cup's title, and a third-division forward
 * who tops its scoring is paid exactly what a first-division one is paid: the prize for scoring
 * in the cup is the cup's, and reading it out of a club's own division would price the same
 * goals three ways according to the shirt. The cup says the same thing in its own legend, in the
 * same rows, because the money is one thing said in two places rather than two things.
 *
 * The rows themselves live in `TopScorerPrizeRows`, which the cup's legend also draws.
 */
const TopScorerPrizePanel: React.FC<{ prize: TopScorerPrizeListDto | null }> = ({ prize }) => {
  if (!prize) return null;

  const isCup = prize.tier == null;
  const shares = prize.rates;

  return (
    <div className="prize-legend top-scorer-prize">
      <h3 className="prize-legend__title">
        {isCup ? <CupTrophy size={18} /> : <DivisionTrophy tier={prize.tier ?? 1} size={18} />}
        Premiação da artilharia — {prize.competitionName}
      </h3>
      <p className="prize-legend__hint">
        {isCup ? (
          <>
            Cada artilheiro da copa leva uma parte do prêmio do campeão da copa, que é de{' '}
            <b>{formatLimo(prize.baseAmount ?? 0)}</b>:{' '}
            {shares.map((share, index) => (
              <span key={share.place}>
                {index > 0 && ' • '}
                {shareLabel(share.place)} {formatRate(share.rate)}
              </span>
            ))}
            . É dinheiro a mais: não sai do prêmio do campeão, e um clube que ganha a copa tendo
            o artilheiro dela leva os dois prêmios.
          </>
        ) : (
          <>
            Cada artilheiro leva uma parte do prêmio do campeão da divisão, que é de{' '}
            <b>{formatLimo(prize.baseAmount ?? 0)}</b>:{' '}
            {shares.map((share, index) => (
              <span key={share.place}>
                {index > 0 && ' • '}
                {shareLabel(share.place)} {formatRate(share.rate)}
              </span>
            ))}
            . O valor é adicional e não sai do prêmio do campeão.
          </>
        )}
      </p>

      <TopScorerPrizeRows prize={prize} />
    </div>
  );
};

export default TopScorerPrizePanel;
