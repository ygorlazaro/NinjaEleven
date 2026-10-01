import React, { useId } from 'react';
import type { KitDto, KitPattern } from '@/types';

/**
 * The eight cuts a shirt may be made in, in the order the editor offers them.
 *
 * The colour is any club's and the cut is not: a plain block, a body with the sleeves cut out
 * of it, stripes, hoops, a sash, halves and a chequer are shirts any club could have turned
 * out in, and a kit drawn in one cut for every club in the country is a swatch rather than a
 * shirt.
 */
export const KIT_PATTERNS: KitPattern[] = [
  'Solid',
  'SolidSeparateSleeves',
  'VerticalStripe',
  'HorizontalStripe',
  'ThinStripes',
  'Sash',
  'Halves',
  'Checkered',
];

/** The name a cut is offered under, in the editor and in the tooltip. */
export const KIT_PATTERN_LABELS: Record<KitPattern, string> = {
  Solid: 'Cheio',
  SolidSeparateSleeves: 'Mangas separadas',
  VerticalStripe: 'Faixa vertical',
  HorizontalStripe: 'Faixa horizontal',
  ThinStripes: 'Listrado',
  Sash: 'Faixa diagonal',
  Halves: 'Metades',
  Checkered: 'Quadriculado',
};

/**
 * The shirt, cut once and drawn in every size.
 *
 * Shoulders, sleeves and hem in one path: a shirt is a shape with a collar and two sleeves, and
 * the shape is the part that has to be right. A CSS box would be a rectangle with two notches,
 * and a rectangle with two notches reads as a picture of a shirt rather than as one.
 */
const BODY = 'M42 14 L60 24 L78 14 L104 30 L92 52 L82 47 L82 106 L38 106 L38 47 L28 52 L16 30 Z';
const COLLAR = 'M50 16 L60 28 L70 16 L66 13 L60 20 L54 13 Z';

/** The torso, which is where a pattern goes. The sleeves are cut separately. */
const TORSO = { left: 38, right: 82, top: 24, bottom: 106 };

/**
 * Black or white, whichever can be read on a colour.
 *
 * A club that has chosen a trim for its number gets its own colour; a club that has not gets
 * the one that works, because a number nobody can read is not a design choice, it is a shirt
 * with the player's name missing from it.
 */
const readableInk = (colour: string): string => {
  const value = colour.replace('#', '');

  if (value.length !== 3 && value.length !== 6) return '#ffffff';

  const full = value.length === 3 ? value.replace(/./g, character => character + character) : value;
  const [red, green, blue] = [0, 2, 4].map(offset => {
    const channel = parseInt(full.slice(offset, offset + 2), 16) / 255;
    return channel <= 0.03928 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue < 0.35 ? '#ffffff' : '#111111';
};

/** A darker version of a colour, for the outline that separates a shirt from its own pattern. */
const outlineOf = (colour: string): string => {
  const value = colour.replace('#', '');
  if (value.length !== 6) return 'rgba(0,0,0,.55)';

  const moved = [0, 2, 4]
    .map(offset => Math.max(0, Math.min(255, parseInt(value.slice(offset, offset + 2), 16) - 34)))
    .map(channel => channel.toString(16).padStart(2, '0'))
    .join('');

  return `#${moved}`;
};

/** Every other band across a span, which is what stripes and hoops both are. */
function bands(count: number, from: number, to: number): { offset: number; length: number }[] {
  const span = to - from;
  const period = span / count;
  const result: { offset: number; length: number }[] = [];

  for (let index = 0; index < count; index += 2) {
    result.push({ offset: index * period, length: period });
  }

  return result;
}

/** The pattern, painted in the club's second colour inside the outline of the shirt. */
function patternOf(pattern: KitPattern, colour: string): React.ReactNode {
  const width = TORSO.right - TORSO.left;
  const height = TORSO.bottom - TORSO.top;

  switch (pattern) {
    case 'SolidSeparateSleeves':
      return (
        <>
          <path d="M42 14 L38 47 L28 52 L16 30 Z" fill={colour} />
          <path d="M78 14 L82 47 L92 52 L104 30 Z" fill={colour} />
        </>
      );
    case 'VerticalStripe':
      return bands(3, 0, width).map((band, index) => (
        <rect key={`v-${index}`} x={TORSO.left + band.offset} y={TORSO.top} width={band.length} height={height} fill={colour} />
      ));
    case 'HorizontalStripe':
      return bands(3, 0, height).map((band, index) => (
        <rect key={`h-${index}`} x={TORSO.left} y={TORSO.top + band.offset} width={width} height={band.length} fill={colour} />
      ));
    case 'ThinStripes':
      return bands(7, 0, width).map((band, index) => (
        <rect key={`t-${index}`} x={TORSO.left + band.offset} y={TORSO.top} width={band.length} height={height} fill={colour} />
      ));
    case 'Sash':
      return <path d="M10 30 L110 74 L110 98 L10 54 Z" fill={colour} />;
    case 'Halves':
      return <rect x={TORSO.left + width / 2} y={TORSO.top - 10} width={width} height={height + 20} fill={colour} />;
    case 'Checkered':
      return Array.from({ length: 4 }, (_, column) =>
        Array.from({ length: 4 }, (_, row) =>
          (column + row) % 2 === 0 ? (
            <rect
              key={`c-${column}-${row}`}
              x={TORSO.left + (column * width) / 4}
              y={TORSO.top + (row * height) / 4}
              width={width / 4}
              height={height / 4}
              fill={colour}
            />
          ) : null,
        ),
      );
    case 'Solid':
    default:
      return null;
  }
}

interface KitShirtProps {
  /** The shirt as the club drew it. */
  kit: KitDto;
  /** The player's number, and the sponsor's name across the chest. */
  number?: number;
  /**
   * How big the number is drawn, in the shirt's own 120-unit space. A shirt on a wall and a
   * shirt beside a name in a list are the same drawing at two sizes, and the number that reads
   * across a room is a smudge at fifteen pixels — so the caller that draws it small asks for
   * a number big enough to still be a number.
   */
  numberSize?: number;
  sponsor?: string;
  /** What the shirt is called underneath it: "Casa", "Fora", or nothing at all. */
  caption?: string;
  /** What the shirt is called for whoever cannot see it. */
  label?: string;
  className?: string;
}

/**
 * A shirt, drawn.
 *
 * <para>
 * Nothing here decides a colour. The two colours and the cut are the club's own, and the third
 * — the one the number and the collar are read in — is the one the club chose for exactly that
 * purpose.
 * </para>
 *
 * <para>
 * The pattern is clipped to the outline of the shirt rather than drawn as a box behind it, so
 * the same cut works on this shape and would work on another: a chequer that spilled past the
 * shoulders would be a rectangle with a shirt drawn on it.
 * </para>
 */
const KitShirt: React.FC<KitShirtProps> = ({ kit, number, numberSize = 30, sponsor, caption, label, className }) => {
  const clipId = useId();
  const body = kit.primaryColor || '#3a6ea5';
  const trim = kit.trimColor || readableInk(body);

  return (
    <figure className={`kit${className ? ` ${className}` : ''}`}>
      <svg
        className="kit__shirt"
        viewBox="0 0 120 120"
        role="img"
        aria-label={label ?? 'Uniforme do clube'}
      >
        <defs>
          <clipPath id={clipId}>
            <path d={BODY} />
          </clipPath>
        </defs>

        <g clipPath={`url(#${clipId})`}>
          <rect x="0" y="0" width="120" height="120" fill={body} />
          {patternOf(kit.pattern, kit.secondaryColor || '#1f3c56')}
        </g>

        <path d={BODY} fill="none" stroke={outlineOf(body)} strokeWidth="2" strokeLinejoin="round" />
        <path d={COLLAR} fill={trim} />

        {number !== undefined && (
          <text x="60" y="84" textAnchor="middle" fontSize={numberSize} fontWeight="800" fill={trim}>
            {number}
          </text>
        )}

        {sponsor && (
          <text x="60" y="48" textAnchor="middle" fontSize="7" fontWeight="700" fill={trim}>
            {sponsor.length > 18 ? `${sponsor.slice(0, 17)}…` : sponsor}
          </text>
        )}
      </svg>
      {caption && <figcaption className="kit__caption">{caption}</figcaption>}
    </figure>
  );
};

export default KitShirt;
