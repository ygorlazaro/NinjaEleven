import React from 'react';

interface MatchControlsProps {
  isPaused: boolean;
  speed: number;
  matchId: string;
  onPause: () => void;
  onResume: () => void;
  onSpeed: (speed: number) => void;
}

const MatchControls: React.FC<MatchControlsProps> = ({ isPaused, speed, onPause, onResume, onSpeed }) => {
  return (
    <div className="controls">
      <div
        className={`ctrl ${speed === 1 ? 'active' : ''}`}
        data-speed="1"
        onClick={() => onSpeed(1)}
      >1×</div>
      <div
        className={`ctrl ${speed === 2 ? 'active' : ''}`}
        data-speed="2"
        onClick={() => onSpeed(2)}
      >2×</div>
      <div
        className={`ctrl ${speed === 4 ? 'active' : ''}`}
        data-speed="4"
        onClick={() => onSpeed(4)}
      >4×</div>

      {isPaused ? (
        <div className="ctrl" onClick={onResume}>▶ Retomar</div>
      ) : (
        <div className="ctrl active" id="pauseBtn" onClick={onPause}>⏸ Pausar</div>
      )}

      <span style={{ marginLeft: 'auto', alignSelf: 'center', color: 'var(--muted)', fontSize: '11px' }}>
        1× ≈ 2:45 • 2× ≈ 1:23 • 4× ≈ 0:41
      </span>
    </div>
  );
};

export default MatchControls;
