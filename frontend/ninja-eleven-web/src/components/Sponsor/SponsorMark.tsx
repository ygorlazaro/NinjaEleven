import type { SponsorMarkDto } from '@/types';
import React from 'react';
import { SponsorLogo } from './SponsorLogo';

interface SponsorMarkProps {
  /** The company on the shirt, or null when the club had no live deal at the whistle. */
  sponsor: SponsorMarkDto | null | undefined;
  /** Rendered height of the panel, in pixels. */
  height?: number;
  className?: string;
}

/**
 * The company on a shirt, as a scoreboard shows it: the mark and the name that goes with it.
 *
 * <para>
 * Both halves are drawn, and they are drawn together, because the panel is the part that
 * identifies a company at a glance and the name is the part a manager reads once. A mark with
 * no name is a badge nobody can place; a name with no mark is a line of text.
 * </para>
 * <para>
 * A club with no sponsor draws nothing at all rather than an empty plate. There is no gap to
 * hold open on a scoreboard for a shirt nobody bought, and a placeholder would be a claim about
 * a club's shirt that is not true.
 * </para>
 */
export function SponsorMark({ sponsor, height = 20, className }: SponsorMarkProps) {
  if (!sponsor) return null;

  return (
    <span className={className ? `sponsor-mark ${className}` : 'sponsor-mark'}>
      <SponsorLogo logo={sponsor.logo} height={height} />
      <span className="sponsor-mark__name">{sponsor.name}</span>
    </span>
  );
}