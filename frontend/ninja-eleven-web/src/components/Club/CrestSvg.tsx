import type {
  CrestDto,
  CrestFigureKind,
  CrestShape
} from '@/types';
import React from 'react';

/**
 * The ten shields, in the order the editor offers them.
 *
 * The shape is the one thing about a crest that is not a colour, and the one thing that makes
 * a club's badge recognisable from across a stand at a size where the lettering is not. So it
 * is a list in one place: a screen that picked its own ten out of a different ten would be a
 * screen offering badges the rest of the game has never heard of.
 */
export const CREST_SHAPES: CrestShape[] = [
  'Round',
  'Oval',
  'Shield',
  'EaredShield',
  'Hexagon',
  'Squircle',
  'Pennant',
  'Banner',
  'Diamond',
  'Star',
];

/** The figures a crest may carry. A crest may carry none, and that is a choice too. */
export const CREST_FIGURES: CrestFigureKind[] = [
  'Ball',
  'Star',
  'Flame',
  'Bolt',
  'Crown',
  'Wave',
  'Sword',
  'Anchor',
];

/**
 * The crest is drawn in a 100 by 120 box, and the field the elements stand on runs from
 * {@link FIELD_TOP} to {@link FIELD_BOTTOM} inside it.
 *
 * The positions are fractions of that field rather than pixels, which is why this mapping is
 * exported: the editor drags an element, and the drag is a number between 0 and 1 that the
 * same crest has to honour at twenty pixels in a table and at a hundred and sixty on the club's
 * own page.
 */
const BOX_HEIGHT = 120;
const FIELD_TOP = 14;
const FIELD_BOTTOM = 108;

export const crestY = (position: number): number =>
  FIELD_TOP + clamp(position, 0, 1) * (FIELD_BOTTOM - FIELD_TOP);

/** The same place, as a percentage of the drawing's height, for an element laid over it. */
export const crestTopPercent = (position: number): number => (crestY(position) / BOX_HEIGHT) * 100;

/**
 * The inverse of {@link crestTopPercent}: how far down a drawing a pointer at that height is
 * holding an element. It is exported for the same reason the other one is — the editor drags an
 * element and a drag has to mean the same place as the drawing it is dragging.
 */
export const crestPositionFromPercent = (percent: number): number =>
  clamp(((percent * BOX_HEIGHT) - FIELD_TOP) / (FIELD_BOTTOM - FIELD_TOP), 0, 1);

/**
 * How near the ends of the field each kind of element may be placed.
 *
 * The position is the middle of the element, so an element dragged to the very top hangs half
 * of itself outside the shield. These are the same two ranges the domain clamps to — the drag
 * stops here so the drawing on screen is the crest that will be stored, rather than a crest the
 * server will quietly pull back into the shield after the manager has watched it be saved.
 */
export const CREST_ELEMENT_RANGE = {
  text: { from: 0.08, to: 0.92 },
  emblem: { from: 0.16, to: 0.84 },
} as const;

const clamp = (value: number, low: number, high: number): number =>
  Number.isFinite(value) ? Math.min(high, Math.max(low, value)) : low;
const round = (value: number): number => Math.round(value * 100) / 100;

/** A five-pointed star as one closed path, because a crest cut in that shape is a real badge. */
function starPath(cx: number, cy: number, outer: number, inner: number): string {
  const points: string[] = [];

  for (let index = 0; index < 10; index += 1) {
    const radius = index % 2 === 0 ? outer : inner;
    const angle = (-90 + index * 36) * (Math.PI / 180);
    points.push(`${round(cx + radius * Math.cos(angle))},${round(cy + radius * Math.sin(angle))}`);
  }

  return `M${points.join(' L')} Z`;
}

/**
 * The outline of each shield, as one path each.
 *
 * Every shape is a path rather than a mixture of paths, circles and rectangles because they are
 * all filled, stroked and clipped the same way: a crest is one shape with a field in it, and a
 * renderer that treated a circle differently from a hexagon is a renderer with two rules for
 * drawing the same thing.
 */
const FIELD_PATHS: Record<CrestShape, string> = {
  Round: 'M50 12 A46 46 0 1 1 49.9 12 Z',
  Oval: 'M50 2 A46 58 0 1 1 49.9 2 Z',
  Shield: 'M50 6 L92 24 L92 60 C92 88 74 104 50 114 C26 104 8 88 8 60 L8 24 Z',
  EaredShield:
    'M50 6 L72 18 L80 2 L92 24 L92 60 C92 88 74 104 50 114 C26 104 8 88 8 60 L8 24 L20 2 L28 18 Z',
  Hexagon: 'M50 6 L88 28 L88 88 L50 110 L12 88 L12 28 Z',
  Squircle:
    'M30 6 H70 C90 6 94 22 94 36 V80 C94 96 88 110 70 110 H30 C12 110 6 96 6 80 V36 C6 22 10 6 30 6 Z',
  Pennant: 'M8 6 H92 L50 114 Z',
  Banner: 'M8 6 H92 V82 L72 108 L50 82 L28 108 L8 82 Z',
  Diamond: 'M50 4 L94 58 L50 112 L6 58 Z',
  Star: starPath(50, 58, 46, 21),
};


/** A regular polygon as one closed path, for the panels of a ball. */
function polygonPath(cx: number, cy: number, radius: number, sides: number, from = -90): string {
  const points: string[] = [];

  for (let index = 0; index < sides; index += 1) {
    const angle = (from + (index * 360) / sides) * (Math.PI / 180);
    points.push(`${round(cx + radius * Math.cos(angle))},${round(cy + radius * Math.sin(angle))}`);
  }

  return `M${points.join(' L')} Z`;
}


/**
 * The figures, drawn in a box from -50 to 50 and then scaled onto the shield.
 *
 * Each one is handed the colour of the field it stands on as well as its own: several of them
 * are a shape with a hole in it — a ball is a ball because of the pentagon in the middle — and a
 * hole has to be the colour of whatever is behind it or the figure stops being that figure.
 */
function Emblem({ kind, color, field }: { kind: CrestFigureKind; color: string; field: string }) {
  const stroke = { fill: 'none', stroke: color, strokeWidth: 11, strokeLinecap: 'round' as const };

  switch (kind) {
    case 'Ball':
      return (
        <g>
          <circle r="46" fill={color} />
          <path d={polygonPath(0, 0, 21, 5)} fill={field} />
          <circle cx="0" cy="-46" r="9" fill={field} />
          <circle cx="40" cy="26" r="9" fill={field} />
          <circle cx="-40" cy="26" r="9" fill={field} />
        </g>
      );
    case 'Star':
      return <path d={starPath(0, 0, 48, 21)} fill={color} />;
    case 'Flame':
      return (
        <g>
          <path d="M0 -50 C28 -18 37 4 23 30 C13 46 -13 46 -23 30 C-37 4 -28 -18 0 -50 Z" fill={color} />
          <path d="M0 -16 C12 0 15 12 7 24 C1 32 -7 32 -12 24 C-19 12 -12 0 0 -16 Z" fill={field} />
        </g>
      );
    case 'Bolt':
      return <path d="M10 -52 L-27 6 L-4 6 L-12 52 L27 -6 L4 -6 Z" fill={color} />;
    case 'Crown':
      return (
        <g>
          <path d="M-46 16 L-46 -26 L-23 4 L0 -32 L23 4 L46 -26 L46 16 Z" fill={color} />
          <rect x="-46" y="16" width="92" height="17" rx="5" fill={color} />
        </g>
      );
    case 'Wave':
      return (
        <g>
          <path d="M-48 -16 Q-24 -40 0 -16 T48 -16" {...stroke} />
          <path d="M-48 16 Q-24 -8 0 16 T48 16" {...stroke} />
        </g>
      );
    case 'Sword':
      return (
        <g>
          <path d="M0 -52 L9 -33 L9 16 L-9 16 L-9 -33 Z" fill={color} />
          <rect x="-29" y="16" width="58" height="10" rx="4" fill={color} />
          <rect x="-5" y="26" width="10" height="17" fill={color} />
          <circle cy="47" r="9" fill={color} />
        </g>
      );
    case 'Anchor':
      return (
        <g>
          <path d="M0 -32 V34" {...stroke} />
          <path d="M-23 -22 H23" {...stroke} />
          <path d="M-38 6 A38 38 0 0 0 38 6" {...stroke} />
          <circle cy="-40" r="8" fill={color} />
        </g>
      );
    default:
      return null;
  }
}

/**
 * The lettering on a crest.
 *
 * The size follows the length rather than being fixed, because a crest carrying three letters
 * and a crest carrying twelve are both a club's name and one of them has to be readable: a name
 * drawn at the size that suits "RBE" runs off the sides of a shield carrying "SPORTING".
 */
function letteringSize(content: string): number {
  return Math.max(8, Math.min(27, 27 - Math.max(0, content.length - 1) * 1.7));
}

interface CrestSvgProps {
  crest: CrestDto;
  /** The club, for the label a screen reader reads and for nothing else. */
  name: string;
  className?: string;
  /** Draws no lettering, for the ten little shields the editor offers as shapes. */
  withoutElements?: boolean;
}

/**
 * A club's badge, drawn.
 *
 * <para>
 * The two colours have one job each and they are not interchangeable: the primary is
 * everything in the foreground — the border, and the lettering unless the club has chosen
 * otherwise — and the secondary is the field the elements sit on. The border is the reason a
 * crest is recognisable at twenty pixels, so it is stroked in the primary on every one of the
 * ten shapes.
 * </para>
 *
 * <para>
 * Each element may carry a third colour of its own, which is not one of the club's two. That is
 * the case a red and black club writing itself in white, and it is why the colour belongs to the
 * element rather than to the crest.
 * </para>
 *
 * <para>
 * One inline SVG rather than a pile of divs, because a crest is a shape with a border and a
 * figure inside it and the shape is the part that has to be right.
 * </para>
 */
const CrestSvg: React.FC<CrestSvgProps> = ({ crest, name, className, withoutElements }) => {
  const field = crest.secondaryColor || '#1f3c56';
  const foreground = crest.primaryColor || '#ffffff';
  const fieldPath = FIELD_PATHS[crest.shape] ?? FIELD_PATHS.Shield;

  return (
    <svg
      className={`club-crest club-crest--drawn${className ? ` ${className}` : ''}`}
      viewBox="0 0 100 120"
      role="img"
      aria-label={`Escudo do ${name}`}
    >
      <path d={fieldPath} fill={field} stroke={foreground} strokeWidth="6" strokeLinejoin="round" />

      {!withoutElements && crest.emblem && (
        <g transform={`translate(50 ${crestY(crest.emblem.verticalPosition)}) scale(0.5)`}>
          <Emblem
            kind={crest.emblem.kind}
            color={crest.emblem.color || foreground}
            field={field}
          />
        </g>
      )}

      {!withoutElements && crest.text && (
        <text
          x="50"
          y={crestY(crest.text.verticalPosition)}
          textAnchor="middle"
          dominantBaseline="middle"
          fontSize={letteringSize(crest.text.content)}
          fontWeight="800"
          letterSpacing="0.02em"
          fill={crest.text.color || foreground}
        >
          {crest.text.content}
        </text>
      )}
    </svg>
  );
};

export default CrestSvg;
