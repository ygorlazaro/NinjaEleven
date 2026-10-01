import React from 'react';
import type { CrestDto } from '@/types';
import CrestSvg from '@/components/Club/CrestSvg';

/**
 * A club's shield: the badge it has drawn, or the initials it is known by when it has none.
 *
 * <para>
 * The two are the same component because a crest and a placeholder occupy the same place in
 * the same layout: the crest and the club's name sit side by side on a scoreboard, in a table
 * cell, in the sidebar and on the club's own page. Drawing the placeholder only where the badge
 * is missing would mean the layout knew in advance, and it does not — a club can be given a
 * badge in the middle of a season and every one of those places has to pick it up.
 * </para>
 *
 * <para>
 * The initials fallback stays because a club with no badge is a normal club, not a broken one.
 * It is a shield in the club's own colours with two letters on it, which is what every club
 * wore here before anybody could draw anything.
 * </para>
 *
 * It lives in its own file because seventeen places draw a club now, and a shield copied into a
 * second place is a shield the two places will stop agreeing about.
 */
const ClubCrest: React.FC<{
  /** The badge, when the club has been given one. */
  crest?: CrestDto | null;
  primary: string;
  secondary: string;
  name: string;
  /** A size or a placement the screen needs; the default shield is 76x88 and not every
   *  table cell can carry one, so a cell shrinks it here rather than in a second shield. */
  className?: string;
}> = ({ crest, primary, secondary, name, className }) => {
  if (crest) {
    return <CrestSvg crest={crest} name={name} className={className} />;
  }

  return (
    <span
      className={`club-crest${className ? ` ${className}` : ''}`}
      style={{ background: primary, borderColor: secondary, color: secondary }}
      role="img"
      aria-label={`Escudo do ${name}`}
      title="Escudo ainda não existe: um placeholder nas cores do clube"
    >
      {name
        .split(' ')
        .filter(word => word.length > 2)
        .slice(0, 2)
        .map(word => word[0])
        .join('')}
    </span>
  );
};

export default ClubCrest;
