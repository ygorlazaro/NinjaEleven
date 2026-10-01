import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import type { KitSide } from '@/types';
import KitChip from '@/components/Club/KitChip';
import EnergyBar from '@/components/Match/EnergyBar';
import { PlayerName } from '@/components/Common/Names';
import { matchRatingClass, matchRatingText, positionLabel, starsToString } from '@/services/formatters';
import HurtBadge from './HurtBadge';

/**
 * One card for a player on the match screen.
 *
 * <para>
 * The eleven under the scoreboard, the two sheets of the matchday and the eleven a manager
 * picks a substitute from are the same card, so they are one card. Three copies of it meant
 * three places where a name could be cut off or a shirt could go missing, and a manager who
 * reads a man on the pitch and then reads him again in the substitution panel was looking at
 * two designs for the same footballer.
 * </para>
 *
 * <para>
 * The shape is the one under the scoreboard, because that is the one being read fastest: the
 * shirt and the name on the first line, and the three facts about him — where he plays, what
 * the evening has been worth, and how good he is — on the second. Everything a particular list
 * has to add goes below, through the slots, so a card can be a card with more on it without
 * becoming a different card.
 * </para>
 */

export interface PlayerCardProps {
  player: MatchPlayerDto;
  /** The club the shirt is drawn from. Without it the card has no shirt, which is a club with
   *  no colours rather than a card that failed to draw one. */
  team?: TeamDto | null;
  side?: KitSide;
  /** What has happened to him in this match, in the row of its own under the numbers. */
  badges?: React.ReactNode;
  /** A further line under the energy bar, for a list that has more to say. */
  detail?: React.ReactNode;
  /** The compact energy bar is the one that sits under a list; the full one has the numbers. */
  compactEnergy?: boolean;
  /** Clicking is a decision, so it is a button and not a div with a handler. */
  onClick?: () => void;
  selected?: boolean;
  disabled?: boolean;
  title?: string;
  className?: string;
}

const PlayerCard: React.FC<PlayerCardProps> = ({
  player,
  team,
  side = 'Home',
  badges,
  detail,
  compactEnergy = true,
  onClick,
  selected = false,
  disabled = false,
  title,
  className = '',
}) => {
  const classes = [
    'player-card',
    'player-tile',
    selected ? 'selected' : '',
    disabled ? 'disabled' : '',
    player.redCard ? 'sent-off' : '',
    player.injuredOff ? 'injured' : '',
    className,
  ]
    .filter(Boolean)
    .join(' ');

  const body = (
    <>
      <span className="player-tile__name">
        <KitChip team={team} side={side} number={player.shirtNumber} size="large" />
        <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
        {/* A man playing through a knock is still on the pitch, so the card that says who is
            on the pitch is also the card that says who is hurt. */}
        <HurtBadge player={player} />
      </span>

      <span className="player-tile__meta">
        <span className="player-tile__pos">
          {player.emergencyGK ? 'GOL*' : positionLabel(player.position)}
        </span>
        {/* The note, and only once there is one. A man who has not played five minutes has a
            cameo and not a match, and no note at all says so rather than implying a score of
            zero. The number and the colour both arrive from the engine. */}
        {player.rating !== null && player.rating !== undefined && (
          <span
            className={`player-rating ${matchRatingClass(player.rating, player.ratingBand)}`}
            title={`Nota da partida: ${player.rating.toFixed(1)} de 10 · ${player.minutesPlayed} min`}
          >
            {matchRatingText(player.rating)}
          </span>
        )}
        <span className="player-tile__stars">{starsToString(player.stars)}</span>
        <span className="player-tile__energy">{Math.round(player.energy)}%</span>
      </span>

      {badges}

      <EnergyBar value={player.energy} compact={compactEnergy} />

      {detail}
    </>
  );

  if (!onClick) {
    return (
      <div className={classes} title={title}>
        {body}
      </div>
    );
  }

  return (
    <button
      type="button"
      className={classes}
      title={title}
      disabled={disabled}
      onClick={onClick}
    >
      {body}
    </button>
  );
};

export default PlayerCard;
