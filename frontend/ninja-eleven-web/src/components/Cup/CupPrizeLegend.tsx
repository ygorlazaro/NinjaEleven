import React from 'react';
import type { CupPrizeDto, TopScorerPrizeListDto } from '@/types';
import { formatLimo } from '@/services/limo';
import CupTrophy from '@/components/Cup/CupTrophy';
import TopScorerPrizeRows, { formatRate, shareLabel } from '@/components/League/TopScorerPrizeRows';

/**
 * What a cup run is worth, said under the bracket it belongs to.
 *
 * **A knockout is paid on the way out.** There is no purse to share between the clubs still in
 * it, so the money is a consolation for the round that knocked you out and a cheque for the
 * winner — and a legend that showed only the winner's would tell a manager what his club is
 * playing for and not what it is playing against.
 *
 * The consolation grows steeply as the round does, and that is the whole point of the list: a
 * club knocked out among the last thirty-two is paid three hundredth of what the finalist is,
 * and the difference between those two lines is the prize for having been in the competition at
 * all. A cup where the first-round loser was paid the same as the finalist would be telling a
 * club its run made no difference.
 *
 * The amounts are the domain's, down to the round that pays nothing, so a client cannot pay a
 * consolation the rules do not have.
 *
 * **The list is a ladder of money and the biggest rung is the champion's.** The lines are ordered
 * by what they pay, from the title down to the club that went out among the last thirty-two, so
 * the top of the panel is the most a cup is worth and the amounts only fall from there. The
 * backend hands the legend in the order the rounds are played, which is a column that rises and
 * falls — five million, ten thousand, three million — and a column with a shape is one a manager
 * reads twice: the finalist's consolation would sit near the bottom with the small money, and the
 * biggest cheque in the competition would have a neighbour a fifth of its size.
 *
 * **The artilharia is in the same list, because it is the same question.** A manager asking what
 * his club is playing for is asking two things and not one: how far the run pays, and what the
 * three men scoring in it are paid for it. The consolation and the artilharia were two panels, one
 * under the other, each with its own title and its own idea of what a cup pays — and a screen
 * that separates them makes a manager read that his cup is worth a champion's purse and stop
 * there. So the cup says it once: the ladder, and then the three strikers and what each of them
 * takes home, drawn in the same rows the division's panel draws them in.
 *
 * **The artilharia is a share of the cup's own champion's prize**, which is the same five million
 * the top of this ladder pays and not a share of any division's purse: the cup runs across the
 * pyramid, so a third-division forward at the top of its scoring is paid exactly what a
 * first-division one is paid. It is also money on top rather than a slice of the champion's
 * cheque — a club that wins the cup with its own top scorer on the sheet is paid the title and
 * the artilharia, because those are two things it did.
 */
const CupPrizeLegend: React.FC<{
  prizes: CupPrizeDto[];
  scorerPrize?: TopScorerPrizeListDto | null;
}> = ({ prizes, scorerPrize }) => {
  if (prizes.length === 0 && !scorerPrize) return null;

  return (
    <div className="prize-legend cup-prize-legend">
      <h3 className="prize-legend__title"><CupTrophy size={18} /> Premiação da copa</h3>
      <p className="prize-legend__hint">
        A copa paga na saída: quem é eliminado leva a cota da fase em que caiu, e quem ganha leva
        o prêmio do título.
      </p>
      {prizes.length > 0 && (
        <div className="cup-prize-legend__rows">
          {byAmount(prizes).map(prize => (
            <div
              key={`${prize.tieRound}-${prize.isChampion}`}
              className={`prize-legend__row ${prize.isChampion ? 'prize-legend__row--champion' : ''}`}
            >
              <span className="prize-legend__position">
                {prize.isChampion ? (
                  <>
                    <CupTrophy size={15} /> Campeão
                  </>
                ) : (
                  `Eliminado em ${roundNameOf(prize)}`
                )}
              </span>
              <span className="prize-legend__amount">{formatLimo(prize.amount)}</span>
            </div>
          ))}
        </div>
      )}

      {scorerPrize && (
        <div className="cup-prize-legend__artilharia">
          <h4 className="prize-legend__subtitle">Artilharia — {scorerPrize.competitionName}</h4>
          <p className="prize-legend__hint">
            {scorerPrize.baseAmount == null ? (
              <>
                Esta copa não tem prêmio de campeão definido, então a artilharia não tem valor
                a repartir. As cotas continuam sendo{' '}
                {scorerPrize.rates.map((share, index) => (
                  <span key={share.place}>
                    {index > 0 && ' • '}
                    {shareLabel(share.place)} {formatRate(share.rate)}
                  </span>
                ))}
                .
              </>
            ) : (
              <>
                Cada artilheiro da copa leva uma parte do prêmio do campeão da copa, que é de{' '}
                <b>{formatLimo(scorerPrize.baseAmount)}</b>:{' '}
                {scorerPrize.rates.map((share, index) => (
                  <span key={share.place}>
                    {index > 0 && ' • '}
                    {shareLabel(share.place)} {formatRate(share.rate)}
                  </span>
                ))}
                . É dinheiro a mais: não sai do prêmio do campeão, e um clube que ganha a copa
                tendo o artilheiro dela leva os dois prêmios.
              </>
            )}
          </p>
          <TopScorerPrizeRows prize={scorerPrize} />
        </div>
      )}
    </div>
  );
};

/**
 * The round a club was knocked out in, in the words the bracket uses: "16 avos de final",
 * "final".
 *
 * The preposition is one for all of them — "eliminado em" — because a club leaves a cup in the
 * phase it reached and the phase is what the line is about. A legend that agreed with the gender
 * of each round ("perdeu na final", "perdeu nos 16 avos") would make the manager read five
 * sentences to learn one thing, and "perdeu" would be the wrong verb for a tie decided on
 * penalties to a club that is not weaker.
 */
const roundNameOf = (prize: CupPrizeDto) => prize.name.toLowerCase();

/**
 * The prizes as a ladder, biggest first, so the champion's cheque is the top line.
 *
 * The order is the screen's and the amounts are not: the sort only decides which line is read
 * first, and the champion's line is the maximum because `PrizeRules` pays the title more than it
 * pays the finalist. A copy, because the array arrives from the backend in the order the rounds
 * are played and that order is the bracket's business, not the panel's.
 */
const byAmount = (prizes: CupPrizeDto[]) =>
  [...prizes].sort((a, b) =>
    b.amount - a.amount || Number(b.isChampion) - Number(a.isChampion));

export default CupPrizeLegend;
