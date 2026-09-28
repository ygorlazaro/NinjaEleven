import React, { useId } from 'react';

/**
 * The cup's trophy, drawn rather than typed.
 *
 * A knockout has one object at the centre of it, and the game's own currency is the emoji — a
 * 🏆 rendered in the browser's own style, which changes with the platform, next to a screen that
 * is otherwise one set of colours. This is the same cup in the game's gold: flat shapes, one
 * light source from the top left, the same outline weight as the shields and the panel borders,
 * and no shadow, because a shadow on a dark panel is a smudge.
 *
 * It is inline SVG rather than a file so it takes `currentColor`'s neighbourhood and the accent,
 * and so it is crisp at any size the two screens that show it ask for.
 */
const CupTrophy: React.FC<{ size?: number; className?: string; title?: string }> = ({
  size = 24,
  className,
  title
}) => {
  // A gradient needs an id, and two trophies on one page (the legend's champion line and the
  // banner under the bracket) would otherwise share one definition by accident.
  const gradientId = useId();

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 32 32"
      fill="none"
      className={className}
      role={title ? 'img' : undefined}
      aria-label={title}
      aria-hidden={title ? undefined : true}
      focusable="false"
    >
      <defs>
        <linearGradient id={gradientId} x1="8" y1="4" x2="24" y2="22" gradientUnits="userSpaceOnUse">
          <stop stopColor="#fbe9a0" />
          <stop offset="0.45" stopColor="#f2d34f" />
          <stop offset="1" stopColor="#c99a12" />
        </linearGradient>
      </defs>

      {title && <title>{title}</title>}

      {/* The handles sit behind the bowl, so the bowl's rim reads as one line across the top. */}
      <path
        d="M8.5 9.5H5a4.5 4.5 0 0 0 0 9h1.5M23.5 9.5H27a4.5 4.5 0 0 1 0 9h-1.5"
        stroke="#c99a12"
        strokeWidth="2.2"
        strokeLinecap="round"
      />

      {/* The bowl: a straight rim that falls away to a point, which is the shape a cup has when
          it is drawn at this size — a rounded one turns into a blob at 16 pixels. */}
      <path
        d="M7.5 5.5h17v1.2c0 6.6-3.9 12.1-8.5 12.1s-8.5-5.5-8.5-12.1V5.5Z"
        fill={`url(#${gradientId})`}
        stroke="#8a6a08"
        strokeWidth="1"
        strokeLinejoin="round"
      />
      {/* The light down the left of the bowl, which is what tells it apart from a shield. */}
      <path d="M10.5 7.5v1c0 4.6 1.9 8.2 4 9.6-1.5-2-2.3-5-2.3-8.6v-2Z" fill="#fff8d8" opacity=".65" />

      {/* The stem and the base: a cup is on a plinth, and the plinth is what a manager imagines
          holding when the number in this list is the one that matters. */}
      <path d="M14.2 18.6h3.6v4.2h-3.6z" fill="#e0b83c" stroke="#8a6a08" strokeWidth="1" />
      <path
        d="M10.5 22.8h11a2 2 0 0 1 2 2v1.9a1.3 1.3 0 0 1-1.3 1.3H9.8a1.3 1.3 0 0 1-1.3-1.3v-1.9a2 2 0 0 1 2-2Z"
        fill="#e0b83c"
        stroke="#8a6a08"
        strokeWidth="1"
      />
    </svg>
  );
};

export default CupTrophy;
