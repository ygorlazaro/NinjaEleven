import React, { useMemo } from 'react';
import { Face } from 'facesjs/react';
import type { FaceConfig } from 'facesjs';

/**
 * A player's face, drawn by the library that made it.
 *
 * The face is not generated here. It arrives from the backend as the JSON of the one that
 * was drawn for this player when the world was seeded, and it is stored because a face is
 * part of who somebody is: a man whose face is redrawn every time his card is opened is not
 * the same man twice, and a manager who met him last week would not recognise him.
 *
 * A player with no face is a real state — a null column — so this renders nothing at all
 * rather than a placeholder, and a face that fails to parse or to draw fails quietly: a
 * nose is never worth taking a profile screen down for.
 */
const parseFace = (face: string | null | undefined): FaceConfig | null => {
  if (!face) return null;

  try {
    const parsed: unknown = JSON.parse(face);

    // A parsed value that is not a face is as broken as one that does not parse. The shape
    // belongs to the library, so this asks only the one question the library cannot answer
    // for us: is there an object here at all.
    if (typeof parsed !== 'object' || parsed === null) return null;

    return parsed as FaceConfig;
  } catch {
    return null;
  }
};

export const PlayerFace: React.FC<{
  face: string | null | undefined;
  /** The width of the portrait in pixels. Height follows the face's own 2:3 shape. */
  size?: number;
  className?: string;
}> = ({ face, size = 96, className }) => {
  const config = useMemo(() => parseFace(face), [face]);

  if (!config) return null;

  return (
    <div
      className={`player-face ${className ?? ''}`}
      style={{ width: size, height: Math.round(size * 1.5) }}
      aria-hidden="true"
    >
      <Face face={config} ignoreDisplayErrors />
    </div>
  );
};
