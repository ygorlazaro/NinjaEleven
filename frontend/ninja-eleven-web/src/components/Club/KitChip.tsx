import React from 'react';
import type { KitSide, TeamDto } from '@/types';
import KitShirt from '@/components/Club/KitShirt';
import { kitOf } from '@/services/clubKits';

interface KitChipProps {
  team: TeamDto | null | undefined;
  /** Which of the club's two shirts. A fixture decides it; a squad page has only one. */
  side?: KitSide;
  /**
   * The number to print on the back, when the shirt is drawn beside a player rather than
   * beside a club. Absent leaves the back blank, which is what a shirt on a wall has.
   */
  number?: number | null;
  /**
   * How big the shirt is drawn. Beside a name in a list it is a mark; on a match card it is
   * the first thing read, because it is the one thing on the card that says which man is
   * which before anybody reads a word.
   */
  size?: 'small' | 'large';
}

/**
 * A club's shirt, at the size of a name.
 *
 * <para>
 * A row of players is a column of names, and a column of names in one club's colours is a
 * column that can be read without reading it: the eleven under a scoreboard, the bench beside
 * it, a squad list. Putting the shirt beside the name says which side of the match a name
 * belongs to and which club a player is at, without a second column of text.
 * </para>
 *
 * <para>
 * It is a shirt rather than a square of colour because the cut is half of what a manager
 * recognises: a club in four thin white stripes is not the club in a solid white, and a swatch
 * would say they were.
 * </para>
 */
const KitChip: React.FC<KitChipProps> = ({ team, side = 'Home', number, size = 'small' }) => {
  const kit = kitOf(team, side);

  if (!team || !kit) return null;

  const which = side === 'Away' ? 'reserva' : 'principal';

  return (
    <span
      className={`kit-chip${number ? ' kit-chip--numbered' : ''}${size === 'large' ? ' kit-chip--large' : ''}`}
      title={`Uniforme ${which} do ${team.name}${number ? `, número ${number}` : ''}`}
    >
      <KitShirt
        kit={kit}
        number={number ?? undefined}
        numberSize={46}
        label={`Uniforme ${which} do ${team.name}${number ? `, número ${number}` : ''}`}
      />
    </span>
  );
};

export default KitChip;
