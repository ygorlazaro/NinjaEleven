import React from 'react';
import { starsToString } from '@/services/formatters';

/**
 * A star rating, drawn one way wherever a rating is shown.
 *
 * The number is the engine's — `PlayerRating` on the server, sent as a plain double — and
 * this only says how to draw it. A rating shown as glyphs in one place and as a number in
 * another is a manager reading two different quantities, and the one next to a player's name
 * is the one that decides whether he is a prospect or a finished article, so it cannot be a
 * figure that means something else.
 *
 * The half star is written as a half-star glyph (⯨) beside the whole ones: there
 * is no half-star glyph that every machine draws perfectly, but modern systems
 * support the LEFT HALF BLACK STAR character.
 */
interface StarRatingProps {
  /** The rating, on the engine's scale. It arrives as a number and is not recomputed here. */
  stars: number;
  /**
   * How big the mark is. The rating beside a name is a badge and the one under a player's
   * attributes is the reading of it, and the same number at two sizes is the same number.
   */
  size?: 'badge' | 'full';
  /** Said beside the mark, so the glyphs are never the only thing on screen. */
  label?: string;
  title?: string;
}

const StarRating: React.FC<StarRatingProps> = ({ stars, size = 'badge', label, title }) => {
  const safe = Number.isFinite(stars) ? Math.max(0, stars) : 0;

  return (
    <span className={`star-rating star-rating--${size}`} title={title}>
      <span className="star-rating__marks" aria-label={`${safe} de 5 estrelas`}>
        {starsToString(safe) || '—'}
      </span>
      {label && <span className="star-rating__label">{label}</span>}
    </span>
  );
};

export default StarRating;
