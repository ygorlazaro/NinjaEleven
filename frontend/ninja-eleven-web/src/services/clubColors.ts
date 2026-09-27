import { useMemo } from 'react';
import type React from 'react';

/**
 * A club's colours, used as a background and as the ink on it.
 *
 * A club picks two colours and they are meant to be worn, not read: the pair that makes a
 * shirt is regularly unreadable as a pair of ink and paper. A yellow home shirt with a
 * white badge gives white-on-yellow text, which is not a style, it is an accident. So the
 * wash is a club colour mixed down into the panel's own dark, and the secondary is only
 * used as ink when it can actually be read against that wash.
 */

interface Rgb {
  r: number;
  g: number;
  b: number;
}

const parseHex = (hex: string | undefined | null, fallback: string): Rgb => {
  const value = (hex ?? '').trim().replace('#', '');

  if (value.length === 3) {
    const [r, g, b] = value.split('');
    return {
      r: parseInt(r + r, 16),
      g: parseInt(g + g, 16),
      b: parseInt(b + b, 16)
    };
  }

  if (value.length >= 6) {
    return {
      r: parseInt(value.slice(0, 2), 16),
      g: parseInt(value.slice(2, 4), 16),
      b: parseInt(value.slice(4, 6), 16)
    };
  }

  return parseHex(fallback, '#f2d34f');
};

const clamp = (value: number) => Math.max(0, Math.min(255, Math.round(value)));

/** How much of the club's colour the panel takes. Enough to be the club, little enough to read. */
export const CLUB_WASH_RATIO = 0.22;

export const PANEL_BASE = '#0a1520';

/** The club's primary pulled down into the panel's own dark, which is what a wash is. */
export const washOf = (primary: string | undefined | null): string => {
  const club = parseHex(primary, '#f2d34f');
  const base = parseHex(PANEL_BASE, '#0a1520');

  return `rgb(${clamp(club.r * CLUB_WASH_RATIO + base.r * (1 - CLUB_WASH_RATIO))}, ${
    clamp(club.g * CLUB_WASH_RATIO + base.g * (1 - CLUB_WASH_RATIO))
  }, ${clamp(club.b * CLUB_WASH_RATIO + base.b * (1 - CLUB_WASH_RATIO))})`;
};

const relativeLuminance = ({ r, g, b }: Rgb): number => {
  const channel = (raw: number) => {
    const value = raw / 255;
    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  };

  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
};

/** The WCAG contrast ratio between two colours, from 1 (identical) to 21 (black on white). */
export const contrast = (foreground: string, background: string): number => {
  const a = relativeLuminance(parseHex(foreground, background));
  const b = relativeLuminance(parseHex(background, foreground));
  const [light, dark] = a > b ? [a, b] : [b, a];

  return (light + 0.05) / (dark + 0.05);
};

/**
 * The club's secondary as ink, when it can be read, and the panel's own text colour when it
 * cannot. The request is for the club to be read in its own colours; the exception is the
 * case where doing so would leave the words unreadable, and an unreadable name is not the
 * club being shown, it is a club hidden.
 */
export const inkOf = (secondary: string | undefined | null, wash: string): string => {
  const candidate = secondary ?? '';

  if (!candidate.trim()) {
    return 'var(--text)';
  }

  return contrast(candidate, wash) >= 3 ? candidate : 'var(--text)';
};

/**
 * The club's window as React custom properties, ready to be spread onto a `style`.
 *
 * It is a hook and not a helper so that the two screens a club is read on — its own and
 * the modal somebody else opened — are given the same two colours by the same measure. A
 * wash a screen computed for itself is a wash the next screen computes differently.
 */
export const useClubWindow = (team: { primaryColor?: string; secondaryColor?: string } | null) => {
  const primary = team?.primaryColor;
  const secondary = team?.secondaryColor;
  const wash = washOf(primary);

  return useMemo(
    () =>
      ({
        '--team-primary': primary || '#f2d34f',
        '--team-secondary': secondary || '#f2d34f',
        '--club-wash': wash,
        '--club-ink': inkOf(secondary, wash)
      }) as React.CSSProperties,
    [primary, secondary, wash]
  );
};
