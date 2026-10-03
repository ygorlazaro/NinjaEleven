import type { SponsorLogoDto, SponsorLogoShape } from '@/types';
import React from 'react';

/**
 * The eight panels a company's name is written on, in the order the backend deals them out.
 *
 * A shape is the one thing about a mark that is not a colour, and the one thing that makes a
 * company recognisable in a column of sixteen shirts. So it is a list in one place, and it is
 * the same eight the game deals: a screen that picked its own would be drawing a different
 * badge for the same company on the shirt and in the sponsor book.
 */
export const SPONSOR_LOGO_SHAPES: SponsorLogoShape[] = [
  'Banner',
  'Block',
  'Roundel',
  'Hexagon',
  'Disc',
  'Plaque',
  'Lozenge',
  'Pennant',
];

/** The lettering, sized down as the words get longer so a long name still fits its panel. */
function fontSizeFor(text: string): number {
  if (text.length <= 8) return 15;
  if (text.length <= 12) return 13;
  if (text.length <= 16) return 11;
  return 9;
}

/**
 * The outline of one panel, in a 120x40 box with its centre at 60,20.
 *
 * Every panel is drawn around the same centre and the same height, so a mark beside a name is
 * the same size whatever shape it is — a panel is an identity, not a measurement.
 */
function panelPath(shape: SponsorLogoShape): string {
  switch (shape) {
    case 'Banner':
      // A hanging cloth: the sides fall away and the bottom is cut in a shallow chevron.
      return 'M6,4 L114,4 L110,34 L60,38 L10,34 Z';
    case 'Block':
      // A plain plate. The plainest panel is the one that has to be read, not recognised.
      return 'M6,6 L114,6 L114,34 L6,34 Z';
    case 'Roundel':
      // A circle inside the plate: the oldest way to put a name on something that belongs to
      // an institution.
      return 'M60,2 m-24,0 a24,24 0 1,0 0,48 a24,24 0 1,0 0,-48';
    case 'Hexagon':
      return 'M22,3 L98,3 L118,20 L98,37 L22,37 L2,20 Z';
    case 'Disc':
      return 'M60,3 a19,17 0 1,0 0,34 a19,17 0 1,0 0,-34';
    case 'Plaque':
      // A mounted plate with a raised edge, cut at the corners.
      return 'M10,7 L110,7 L116,13 L116,33 L10,33 L4,27 L4,13 Z';
    case 'Lozenge':
      return 'M60,3 L118,20 L60,37 L2,20 Z';
    case 'Pennant':
      // A club's flag: a mast down the left and a swallow-tailed field.
      return 'M8,2 L8,38 M8,5 L112,5 L96,20 L112,35 L8,35';
    default:
      return 'M6,6 L114,6 L114,34 L6,34 Z';
  }
}

/**
 * One panel that does not close on itself, and so has to be stroked rather than filled — the
 * pennant's mast and its outline, which is what makes it read as a flag and not as a shape.
 */
function isOpenPanel(shape: SponsorLogoShape): boolean {
  return shape === 'Pennant';
}

interface SponsorLogoProps {
  logo: SponsorLogoDto;
  /** Rendered height in pixels; the mark keeps its 3:1 proportion at any of them. */
  height?: number;
  className?: string;
  /** Announced to a screen reader when the mark is the only place the company is named. */
  title?: string;
}

/**
 * A company's mark, drawn as the backend described it.
 *
 * <para>
 * The panel, the two colours and the words all arrive from the world; nothing here decides
 * anything. That is deliberate — the same mark has to appear on a scoreboard, in the sponsor
 * book and on a transfer card, and a mark drawn three times is three marks that eventually
 * disagree.
 * </para>
 */
export function SponsorLogo({ logo, height = 20, className, title }: SponsorLogoProps) {
  const width = Math.round(height * 3);
  const open = isOpenPanel(logo.shape);

  return (
    <svg
      className={className}
      width={width}
      height={height}
      viewBox="0 0 120 40"
      role={title ? 'img' : 'presentation'}
      aria-label={title}
      aria-hidden={title ? undefined : true}
    >
      {title ? <title>{title}</title> : null}
      <path
        d={panelPath(logo.shape)}
        fill={open ? 'none' : logo.backgroundColor}
        stroke={open ? logo.backgroundColor : logo.inkColor}
        strokeWidth={open ? 7 : 1.5}
        strokeLinejoin="round"
        strokeLinecap="round"
      />
      <text
        x="60"
        y="20"
        textAnchor="middle"
        dominantBaseline="central"
        fontFamily="'Trebuchet MS', 'Segoe UI', sans-serif"
        fontSize={fontSizeFor(logo.text)}
        fontWeight="700"
        letterSpacing="0.3"
        fill={logo.inkColor}
      >
        {logo.text}
      </text>
    </svg>
  );
}