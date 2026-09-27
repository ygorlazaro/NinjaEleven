import React from 'react';
import type { MatchPlayerDto } from '@/types';

interface HurtBadgeProps {
  player: MatchPlayerDto;
  /** The icon sits in a different row on each card, so it is not always the same wrapper. */
  className?: string;
}

/**
 * The bandage beside the name of a man who is hurt and still on his feet.
 *
 * A knock has two faces in this model and they are not the same news: a light injury is a
 * player who stays down and carries on, and a grave one is a player who leaves. A card that
 * only knows about the second shows a healthy eleven while four men play through a limp, so
 * the severity travels with the player and this is where it is drawn.
 *
 * It is next to the name and nowhere else. The manager reads a team sheet looking for men,
 * not for a legend, and a badge that had to be hunted for in a row of numbers would be a
 * badge nobody sees. A player who has gone off does not get one: the ambulance already has
 * him, and two icons saying the same thing is noise.
 */
const HurtBadge: React.FC<HurtBadgeProps> = ({ player, className }) => {
  if (player.injury === 'None' || player.injuredOff) {
    return null;
  }

  return (
    <span
      className={['injury-mark', 'hurt', className].filter(Boolean).join(' ')}
      title="Jogando lesionado"
      aria-label="Jogando lesionado"
    >
      🩹
    </span>
  );
};

export default HurtBadge;
