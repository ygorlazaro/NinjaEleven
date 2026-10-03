import React from 'react';
import type { TransferRankingEntryDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';

/**
 * The box a club wears in the four transfer rankings.
 *
 * <para>
 * The market's other three tabs are grids of men, so the rankings are a grid of clubs in the
 * same shape: the shield on the left and the facts to its right, in the same order, on the same
 * grid. A screen with four tables under a grid of boxes is a screen where the manager has to
 * learn the layout twice, and the second time is in the place where he is comparing what other
 * clubs have spent.
 * </para>
 *
 * <para>
 * It is a box and not a <c>PlayerBox</c> because it is not a man. The shape is shared and the
 * contents are not, and a box that drew a portrait for a club would be a box that had stopped
 * saying what it was showing.
 * </para>
 */
const ClubBox: React.FC<{
  entry: TransferRankingEntryDto;
  /** Its place in this ranking, counted from one. */
  place: number;
  /** What the first number counts, in the words the ranking is about. */
  countLabel: string;
  /** What the second number is, in the words the ranking is about. */
  amountLabel: string;
  formatAmount: (value: number) => string;
}> = ({ entry, place, countLabel, amountLabel, formatAmount }) => (
  <article className="player-box">
    <div className="player-box__portrait player-box__portrait--crest">
      <ClubCrest
        primary={entry.primaryColor}
        secondary={entry.secondaryColor}
        name={entry.teamName}
        className="player-box__crest-lg"
      />
    </div>

    <div className="player-box__body">
      {/* The place is beside the name and not in a column of its own, because on a grid the
          first box of a ranking and the fifth are read side by side and a place a manager has
          to count across is a place he counts wrong. */}
      <div className="player-box__name">
        <span className="player-box__place" title={`${place}º lugar`}>
          {place}º
        </span>
        <ClubName teamId={entry.teamId}>{entry.teamName}</ClubName>
      </div>

      <div className="player-box__line player-box__line--money">
        <span>
          <span className="player-box__tag">{countLabel}</span> {entry.transfers}
        </span>
        <span>
          <span className="player-box__tag">{amountLabel}</span> {formatAmount(entry.amount)}
        </span>
      </div>
    </div>
  </article>
);

interface TransferRankingListProps {
  title: string;
  entries: TransferRankingEntryDto[];
  metricLabel: string;
  formatMetric: (value: number) => string;
}

/**
 * One of the four rankings of the division's transfer business, as a grid of clubs.
 *
 * The four are four questions about the same sixteen clubs, and a manager comparing what one
 * division spent against what another division bought is comparing them box by box.
 */
const TransferRankingList: React.FC<TransferRankingListProps> = ({
  title,
  entries,
  metricLabel,
  formatMetric
}) => (
  <section className="transfer-ranking-table">
    <h4>{title}</h4>
    {entries.length === 0 ? (
      <p className="transfer-empty">Nenhuma movimentação registrada.</p>
    ) : (
      <div className="player-box-grid">
        {entries.map((entry, i) => (
          <ClubBox
            key={entry.teamId}
            entry={entry}
            place={i + 1}
            countLabel={metricLabel}
            amountLabel="Total"
            formatAmount={formatMetric}
          />
        ))}
      </div>
    )}
  </section>
);

/**
 * The money the rankings count is formatted by the caller and not here: two of the four are
 * money and two are counts, and a shared formatter would have to be told which is which on
 * every call — a second opinion about a number that the screen was already given.
 */
export default TransferRankingList;