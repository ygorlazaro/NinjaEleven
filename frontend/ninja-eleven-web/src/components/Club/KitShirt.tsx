import React, { useMemo } from 'react';

/**
 * The layout a shirt is cut in.
 *
 * The colour is a club's and the cut is not: a plain block, a set of stripes, a band or a
 * split down the middle are four shirts that any club could have turned out, and a kit drawn
 * in one layout for every club in the world is a swatch and not a shirt. The cut is drawn
 * from the club's own id, so a manager who changes clubs sees a different shirt rather than
 * the same one in a different colour.
 */
type KitLayout = 'solid' | 'stripes' | 'hoop' | 'halves';

/**
 * How many stripes a striped shirt has, and how wide. A club's stripes are the number of
 * stripes and the width of the gap between them, and the difference is the whole of a kit: one
 * wide band is a different club from four thin ones.
 */
const LAYOUTS: KitLayout[] = ['solid', 'stripes', 'hoop', 'halves'];

const STRIPE_COUNTS: Record<KitLayout, number> = {
  solid: 0,
  stripes: 4,
  hoop: 3,
  halves: 0
};

interface KitShirtProps {
  /** Which way round: the home shirt is the one a manager sees most, so it is the first. */
  variant: 'home' | 'away';
  /** The body's colour. */
  primary: string;
  /** The trim, the number and the other half of a split shirt. */
  secondary: string;
  /** The club, whose id decides the cut. Stable: the same club is cut the same way. */
  seed: string;
  /** The shirt's number, and the sponsor's name across the chest in a full kit. */
  number?: number;
  sponsor?: string;
}

const shade = (hex: string, amount: number): string => {
  const value = (hex ?? '').trim().replace('#', '');

  if (value.length < 6) {
    return hex;
  }

  const channel = (offset: number) => {
    const raw = parseInt(value.slice(offset, offset + 2), 16);
    const moved = Math.max(0, Math.min(255, raw + amount));
    return moved.toString(16).padStart(2, '0');
  };

  return `#${channel(0)}${channel(2)}${channel(4)}`;
};

/**
 * A shirt, drawn.
 *
 * The whole shirt is one inline SVG rather than a pile of nested divs, because a shirt is a
 * shape with a collar and two sleeves and the shape is the part that has to be right: a CSS
 * box would be a rectangle with two notches, and a rectangle with two notches reads as a
 * picture of a shirt rather than as one.
 *
 * Nothing here decides a colour. The two colours are the club's and the cut is drawn from the
 * club's id, and a shirt that took a third colour would be wearing something the club does not
 * own.
 */
const KitShirt: React.FC<KitShirtProps> = ({ variant, primary, secondary, seed, number, sponsor }) => {
  const layout = useMemo(() => {
    const digest = [...seed].reduce((sum, character) => sum + character.charCodeAt(0), 0);

    return LAYOUTS[(digest + (variant === 'away' ? 1 : 0)) % LAYOUTS.length];
  }, [seed, variant]);

  const body = variant === 'away' ? secondary : primary;
  const trim = variant === 'away' ? primary : secondary;
  const stripes = STRIPE_COUNTS[layout];
  const dark = shade(body, -28);
  const bandTop = 26;
  const bandHeight = 100 / (stripes * 2 + 1);

  return (
    <figure className={`kit kit--${variant}`}>
      <svg
        className="kit__shirt"
        viewBox="0 0 120 120"
        role="img"
        aria-label={`Uniforme ${variant === 'home' ? 'de casa' : 'fora'}`}
      >
        {/* The body: shoulders, sleeves and hem, in the club's colour. */}
        <path
          d="M42 14 L60 24 L78 14 L104 30 L92 52 L82 47 L82 106 L38 106 L38 47 L28 52 L16 30 Z"
          fill={body}
          stroke={dark}
          strokeWidth="2"
        />

        {/* The cut, which is the part that makes a shirt a club's. */}
        {layout === 'stripes' &&
          Array.from({ length: stripes }).map((_, index) => (
            <rect
              key={`stripe-${index}`}
              x="38"
              y={bandTop + index * bandHeight * 2 + bandHeight * 0.5}
              width="44"
              height={bandHeight * 0.9}
              fill={trim}
            />
          ))}

        {layout === 'hoop' &&
          Array.from({ length: stripes }).map((_, index) => (
            <rect
              key={`hoop-${index}`}
              x="38"
              y={bandTop + index * bandHeight * 2 + bandHeight * 0.6}
              width="44"
              height={bandHeight * 0.75}
              fill={trim}
            />
          ))}

        {layout === 'halves' && <rect x="60" y="14" width="22" height="92" fill={trim} />}

        {/* The collar, in the trim, and the number on the back. */}
        <path d="M50 16 L60 28 L70 16 L66 13 L60 20 L54 13 Z" fill={trim} />
        {number !== undefined && (
          <text
            x="60"
            y="76"
            textAnchor="middle"
            fontSize="30"
            fontWeight="800"
            fill={trim}
          >
            {number}
          </text>
        )}
        {sponsor && (
          <text x="60" y="46" textAnchor="middle" fontSize="7" fontWeight="700" fill={trim}>
            {sponsor.length > 18 ? `${sponsor.slice(0, 17)}…` : sponsor}
          </text>
        )}
      </svg>
      <figcaption className="kit__caption">
        {variant === 'home' ? 'Casa' : 'Fora'}
      </figcaption>
    </figure>
  );
};

export default KitShirt;
