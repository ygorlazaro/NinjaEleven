import React from 'react';

interface MatchControlsProps {
  isPaused: boolean;
  /** The pace the backend is running the match at. It is the only truth about it. */
  speed: number;
  muted: boolean;
  onPause: () => void;
  onResume: () => void;
  onToggleMuted: () => void;
}

/**
 * Pause and resume, the pace read-only, and the sound. The manager does not choose how fast
 * the match runs any more: the backend decides and publishes it, and the screen only says
 * what it is. The same goes for the sound — the crowd is part of the match, and the only
 * decision is whether to hear it.
 */
const MatchControls: React.FC<MatchControlsProps> = ({
  isPaused,
  speed,
  muted,
  onPause,
  onResume,
  onToggleMuted,
}) => {
  return (
    <div className="controls">
      {isPaused ? (
        <div className="ctrl" onClick={onResume}>▶ Retomar</div>
      ) : (
        <div className="ctrl active" id="pauseBtn" onClick={onPause}>⏸ Pausar</div>
      )}

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
