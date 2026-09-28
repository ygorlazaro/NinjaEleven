import React from 'react';
import type { DivisionPurseDto } from '@/types';
import { formatLimo } from '@/services/limo';
import DivisionTrophy from '@/components/League/DivisionTrophy';

/**
 * What the division on screen is worth, said under the calendar.
 *
 * It follows the division the table above is showing, because the money is a division's money: a
 * first division paid three times a third is three positions that look identical on two tables
 * and are worth three different cheques, so the legend of the third division under the first
 * division's table would be a legend about the wrong football.
 *
 * **Every position is listed.** A manager reading his table wants to know what ninth place is
 * worth as much as what the title is, and a legend that only shows the podium answers a question
 * nobody asked — the one asked at the bottom of the table.
 *
 * **The numbers arrive from the backend.** The share of a purse is a geometric weight and the
 * last club is paid whatever the other eleven leave over, so a screen that divided the purse by
 * twelve would publish a championship that does not pay out its own money — and the last cheque
 * is the one a manager adds the other eleven up to check.
 *
 * **The title's line carries the division's own cup.** The first division's trophy is not the
 * third's with a different label on it, and a legend that showed the same mark for three tables
 * would be telling a manager that winning the 3ª Divisão is worth the same thing as winning the
 * 1ª, which is the opposite of what the panel underneath it says. The tier comes from the purse
 * the backend sent, so the cup on the line is the cup of the division being paid.
 */
const PrizeLegend: React.FC<{ purses: DivisionPurseDto[]; tier?: number | null }> = ({ purses, tier }) => {
  const purse = tier == null ? undefined : purses.find(candidate => candidate.tier === tier);

  if (!purse) return null;

  return (
    <div className="prize-legend">
      <h3 className="prize-legend__title">💰 Premiação — {purse.name}</h3>
      <p className="prize-legend__hint">
        A bolsa da divisão é <b>{formatLimo(purse.purse)}</b>, dividida entre os {purse.clubs}{' '}
        clubes pelo peso da posição. As quatro rebaixadas também recebem: jogaram a temporada
        inteira.
      </p>
      <div className="prize-legend__rows">
        {purse.shares.map(share => (
          <div
            key={share.position}
            className={`prize-legend__row ${share.position === 1 ? 'prize-legend__row--champion' : ''}`}
          >
            <span className="prize-legend__position">
              {share.position === 1 && <DivisionTrophy tier={purse.tier} size={15} />}
              {positionLabel(share.position)}
            </span>
            <span className="prize-legend__amount">{formatLimo(share.amount)}</span>
          </div>
        ))}
      </div>
    </div>
  );
};

/**
 * The two positions that have names in football, and a number for the other ten. A column of
 * twelve identical "lugar" is a column nobody scans, and the champion and the runner-up are the
 * two a manager refers to by what they are rather than by where they finished.
 */
const positionLabel = (position: number) => {
  if (position === 1) return 'Campeão';
  if (position === 2) return 'Vice';
  return `${position}º lugar`;
};

export default PrizeLegend;
