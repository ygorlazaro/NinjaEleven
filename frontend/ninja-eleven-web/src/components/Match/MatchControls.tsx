import React from 'react';

interface MatchControlsProps {
  /** The pace the backend is running the match at. It is the only truth about it. */
  speed: number;
  muted: boolean;
  onToggleMuted: () => void;
}

/**
 * The pace, read-only, and the sound.
 *
 * <para>
 * The manager does not choose how fast the match runs, and he does not stop it either. Both
 * used to be buttons here and both are gone: a match is opened by the world's calendar rather
 * than by the hand on this screen, so a pause pressed here would stop a match for everybody
 * looking at it and for nobody deciding anything — the match would go on the moment the page
 * was refreshed, and the manager would be left with a button that does not mean what it says.
 * </para>
 *
 * <para>
 * What is left is the two things a spectator is genuinely allowed to change: how fast the
 * words arrive, which is the backend's answer read out, and whether to hear them.
 * </para>
 */
const MatchControls: React.FC<MatchControlsProps> = ({ speed, muted, onToggleMuted }) => {
  return (
    <div className="controls">
      <span className="ctrl readonly" title="O ritmo é definido pelo servidor">
        {speed}×
      </span>

      <div
        className={`ctrl ${muted ? '' : 'active'}`}
        onClick={onToggleMuted}
        title={muted ? 'Ligar o som' : 'Desligar o som'}
        style={{ marginLeft: 'auto' }}
      >
        {muted ? '🔇 Som' : '🔊 Som'}
      </div>
    </div>
  );
};

export default MatchControls;
