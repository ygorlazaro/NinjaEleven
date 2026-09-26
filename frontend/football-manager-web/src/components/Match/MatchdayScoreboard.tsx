import React from 'react';

/** Live score of one match of the round, as pushed by the backend. */
export interface MatchScore {
  roundId: string;
  matchId: string;
  fixtureId: string;
  homeTeamId: string;
  homeTeamName: string;
  homeShortName: string;
  awayTeamId: string;
  awayTeamName: string;
  awayShortName: string;
  homeGoals: number;
  awayGoals: number;
  minute: number;
  half: string;
  status: string;
  isFinished: boolean;
}

interface MatchdayScoreboardProps {
  scores: MatchScore[];
  /** Match the client is watching in full; it is shown apart from the others. */
  currentMatchId: string;
  userTeamId?: string;
}

/**
 * The other matches of the round, updated over the same connection that carries the
 * match being watched. The four matches of a matchday run together on the backend, so
 * this is the same clock, not a second source of truth.
 */
const MatchdayScoreboard: React.FC<MatchdayScoreboardProps> = ({
  scores,
  currentMatchId,
  userTeamId,
}) => {
  const others = scores.filter(score => score.matchId !== currentMatchId);
  if (others.length === 0) return null;

  return (
    <div className="league-panel">
      <h3>📡 Outras partidas da rodada</h3>
      <div className="fixture-list">
        {others.map(score => {
          const involvesUser = userTeamId && (score.homeTeamId === userTeamId || score.awayTeamId === userTeamId);
          const minute = score.isFinished ? 'FIM' : `${score.minute}'`;

          return (
            <div
              key={score.matchId}
              className={`fixture ${score.isFinished ? 'played' : 'live'} ${involvesUser ? 'user-fixture' : ''}`}
            >
              <span>{score.isFinished ? '✓' : '●'}</span>
              <span className="home" style={{ color: '#9fb6c8' }} title={score.homeTeamName}>{score.homeTeamName}</span>
              <span className="result">
                {score.homeGoals} × {score.awayGoals}
                <span style={{ color: 'var(--muted)', fontSize: '9px', marginLeft: '5px' }}>{minute}</span>
              </span>
              <span className="away" style={{ color: '#9fb6c8' }} title={score.awayTeamName}>{score.awayTeamName}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default MatchdayScoreboard;
