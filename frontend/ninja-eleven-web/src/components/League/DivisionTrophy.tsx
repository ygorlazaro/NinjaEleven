import React, { useId } from 'react';

/**
 * The three trophies of the pyramid, one per division, and the further up the pyramid the more
 * there is to a trophy.
 *
 * A championship's table is a ladder, and the ladder has three rungs: the first division's is a
 * lidded cup on a stepped plinth, the second's is the same cup without the lid, and the third's
 * is a plain bowl on a short base. That is not decoration for its own sake — it is the shape of
 * the pyramid said in one glance before a single number is read: the table above mine has the
 * bigger prize and it looks like it.
 *
 * They are drawn rather than typed for the same reason the cup's trophy is: an emoji is rendered
 * in the platform's own style, next to a screen that is one set of colours, and the three
 * divisions' trophies would then be the same object in three sizes. This is one component with
 * three drawings, sharing the game's gold and one light from the top left.
 */
const DivisionTrophy: React.FC<{ tier: number; size?: number; className?: string }> = ({
  tier,
  size = 24,
  className
}) => {
  // A gradient needs an id, and three trophies on one page would otherwise share one
  // definition by accident.
  const gradientId = useId();

  // Anything that is not the first division is not the first division: the tier is a number
  // from a season's own list of divisions, and a screen with its own copy of how many there are
  // is a screen that is wrong the day a fourth one is added.
  const shape = tier <= 1 ? 'first' : tier === 2 ? 'second' : 'third';

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 32 36"
      fill="none"
      className={className}
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={gradientId} x1="9" y1="4" x2="23" y2="30" gradientUnits="userSpaceOnUse">
          <stop stopColor="#fbe9a0" />
          <stop offset="0.45" stopColor="#f2d34f" />
          <stop offset="1" stopColor="#c99a12" />
        </linearGradient>
      </defs>

      {/* Handles sit behind the bowl, so the rim reads as one line across the top. */}
      {shape === 'third' ? (
        <path
          d="M9.5 12H7.5a3.5 3.5 0 0 0 0 7h1M22.5 12h2a3.5 3.5 0 0 1 0 7h-1"
          stroke="#c99a12"
          strokeWidth="1.8"
          strokeLinecap="round"
        />
      ) : (
        <path
          d="M8.5 10.5H5a5 5 0 0 0 0 10h1.5M23.5 10.5H27a5 5 0 0 1 0 10h-1.5"
          stroke="#c99a12"
          strokeWidth="2.2"
          strokeLinecap="round"
        />
      )}

      {/* The lid and the knob: the first division alone has one, and it is the whole difference
          between a title and a cup. */}
      {shape === 'first' && (
        <>
          <circle cx="16" cy="4" r="1.7" fill="#e0b83c" stroke="#8a6a08" strokeWidth="0.9" />
          <path
            d="M10.4 6.2h11.2l1.4 2.6H9l1.4-2.6Z"
            fill="#e0b83c"
            stroke="#8a6a08"
            strokeWidth="1"
            strokeLinejoin="round"
          />
        </>
      )}

      <path
        d={shape === 'third' ? 'M9 8.5h14v1c0 5.4-3.1 9.8-7 9.8s-7-4.4-7-9.8v-1Z'
          : 'M7.5 8.8h17v1c0 6.6-3.9 12.1-8.5 12.1s-8.5-5.5-8.5-12.1v-1Z'}
        fill={`url(#${gradientId})`}
        stroke="#8a6a08"
        strokeWidth="1"
        strokeLinejoin="round"
      />
      {/* The light down the left of the bowl, which is what tells a cup apart from a shield. */}
      <path
        d={shape === 'third' ? 'M11.5 10.4v.9c0 3.6 1.3 6.4 2.8 7.6-1-1.7-1.5-3.9-1.5-6.5v-2Z'
          : 'M10.5 10.8v1c0 4.6 1.9 8.2 4 9.6-1.5-2-2.3-5-2.3-8.6v-2Z'}
        fill="#fff8d8"
        opacity=".65"
      />
      {/* The star on the first division's bowl, and the rings on the first two stems. */}
      {shape === 'first' && (
        <path d="M16 14.2l1.15 2.33 2.57.37-1.86 1.81.44 2.56L16 19.99l-2.3 1.28.44-2.56-1.86-1.81 2.57-.37L16 14.2Z" fill="#8a6a08" opacity=".55" />
      )}

      {shape === 'third' ? (
        <path d="M14.6 19.3h2.8v3.1h-2.8z" fill="#e0b83c" stroke="#8a6a08" strokeWidth="1" />
      ) : (
        <>
          <path d="M14.2 21.9h3.6v2.2h-3.6z" fill="#e0b83c" stroke="#8a6a08" strokeWidth="1" />
          {shape === 'first' && (
            <path d="M13.6 24.1h4.8v2.2h-4.8z" fill="#e0b83c" stroke="#8a6a08" strokeWidth="1" />
          )}
        </>
      )}

      {/* The base: a plinth under a step, a step under a plain slab, and one slab for the third
          division — the width and the number of steps are the rank. */}
      {shape === 'first' ? (
        <>
          <path d="M11 26.3h10v2.4H11z" fill="#e0b83c" stroke="#8a6a08" strokeWidth="1" />
          <path
            d="M8.6 28.7h14.8a2 2 0 0 1 2 2v2.1a1.3 1.3 0 0 1-1.3 1.3H7.9a1.3 1.3 0 0 1-1.3-1.3v-2.1a2 2 0 0 1 2-2Z"
            fill="#e0b83c"
            stroke="#8a6a08"
            strokeWidth="1"
          />
        </>
      ) : shape === 'second' ? (
        <>
          <path d="M12.5 24.1h7v2.4h-7z" fill="#e0b83c" stroke="#8a6a08" strokeWidth="1" />
          <path
            d="M10 26.5h12a2 2 0 0 1 2 2v2.1a1.3 1.3 0 0 1-1.3 1.3H9.3a1.3 1.3 0 0 1-1.3-1.3v-2.1a2 2 0 0 1 2-2Z"
            fill="#e0b83c"
            stroke="#8a6a08"
            strokeWidth="1"
          />
        </>
      ) : (
        <path
          d="M11 22.4h10a1.8 1.8 0 0 1 1.8 1.8v1.9a1.3 1.3 0 0 1-1.3 1.3h-11a1.3 1.3 0 0 1-1.3-1.3v-1.9a1.8 1.8 0 0 1 1.8-1.8Z"
          fill="#e0b83c"
          stroke="#8a6a08"
          strokeWidth="1"
        />
      )}
    </svg>
  );
};

export default DivisionTrophy;
