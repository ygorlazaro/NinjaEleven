import React from 'react';
import type { FixtureDto } from '@/types';

interface FixtureListProps {
  fixtures: FixtureDto[];
  userId?: string;
  onSelect?: (fixture: FixtureDto) => void;
}

/** The backend status a fixture can be in. */
const isPlayed = (f: FixtureDto) => f.status === 'Finished';
const isLive = (f: FixtureDto) => f.status === 'InProgress';

const FixtureList: React.FC<FixtureListProps> = ({ fixtures, userId, onSelect }) => {
  const result = (f: FixtureDto): string =>
    f.homeGoals !== null && f.homeGoals !== undefined && f.awayGoals !== null && f.awayGoals !== undefined
      ? `${f.homeGoals} × ${f.awayGoals}`
      : '—';

  // A fixture is watched when it has a match: that is the only way back into a match
  // that is being played or already finished.
  const target = (f: FixtureDto): string | null => {
    if (f.matchId) return `/match/${f.matchId}`;
    if (f.status === 'Scheduled' && (f.homeTeamId === userId || f.awayTeamId === userId)) {
      return `/match/lineup/${f.id}`;
    }
    return null;
  };

  if (fixtures.length === 0) {
    return <div className="league-empty">Nenhum jogo nesta rodada.</div>;
  }

  return (
    <div className="fixture-list">
      {fixtures.map(f => {
        const home = f.homeTeam;
        const away = f.awayTeam;
        const isUser = f.homeTeamId === userId || f.awayTeamId === userId;
        const homeColor = home?.primaryColor || '#57a6ff';
        const awayColor = away?.primaryColor || '#ff647c';
        const link = target(f);
        const state = isPlayed(f) ? 'played' : isLive(f) ? 'live' : '';

        return (
          <div
            key={f.id}
            className={`fixture ${isUser ? 'user-fixture' : ''} ${state} ${link ? 'clickable' : ''}`}
            style={{ opacity: isPlayed(f) ? 0.78 : 1 }}
            title={link ? (isPlayed(f) ? 'Assistir de novo' : isLive(f) ? 'Assistir ao vivo' : 'Escalar e jogar') : undefined}
            onClick={link && onSelect ? () => onSelect(f) : undefined}
          >
            <span>{isPlayed(f) ? '✓' : isLive(f) ? '●' : ''}</span>
            <span className="home" style={{ color: homeColor }}>
              {home?.name || 'Casa'}
            </span>
            <span className="result">{result(f)}</span>
            <span className="away" style={{ color: awayColor }}>
              {away?.name || 'Fora'}
            </span>
          </div>
        );
      })}
    </div>
  );
};

export default FixtureList;
