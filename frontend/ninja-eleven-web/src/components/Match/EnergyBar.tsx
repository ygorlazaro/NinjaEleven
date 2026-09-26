import React from 'react';
import { energyClass } from '@/services/formatters';

interface EnergyBarProps {
  /** Energy left, 0 to 100. Values outside the range are clamped rather than trusted. */
  value: number;
  /** Hides the track border and the rounding, for the very tight spots. */
  compact?: boolean;
}

/**
 * The energy bar, in one place. It owns both the track and the fill on purpose: a fill
 * alone has no height to be a percentage of and no width to be a percentage of, which is
 * how the bar ended up unformatted in half the places it was used. Whoever wants a bar
 * uses this, so there is only one shape to get right.
 */
const EnergyBar: React.FC<EnergyBarProps> = ({ value, compact = false }) => {
  const clamped = Math.max(0, Math.min(100, Math.round(value)));

  return (
    <div
      className={`pc-energy ${compact ? 'pc-energy--compact' : ''}`}
      role="progressbar"
      aria-valuenow={clamped}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={`Energia ${clamped}%`}
    >
      <div className={`pc-energy-fill ${energyClass(clamped)}`} style={{ width: `${clamped}%` }} />
    </div>
  );
};

export default EnergyBar;
