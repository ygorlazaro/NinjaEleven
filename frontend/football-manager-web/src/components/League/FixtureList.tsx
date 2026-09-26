import React from 'react';
import type { FixtureDto, TeamDto } from '@/types';

interface FixtureListProps {
  fixtures: FixtureDto[];
  userId?: string;
}

const FixtureList: React.FC<FixtureListProps> = ({ fixtures, userId }) => {
  const formatResult = (f: FixtureDto): string => {
    if (f.homeGoals !== null && f.homeGoals !== undefined && f.awayGoals !== null && f.awayGoals !== undefined) {
      return `${f.homeGoals} × ${f.awayGoals}`;
    }
    return '—';
  };

  return (
    <div className="fixture-list">
      {fixtures.length === 0 ? (
        <div className="league-empty">Nenhum jogo nesta rodada.</div>
      ) : (
        fixtures.map(f => {
          const home = f.homeTeam;
          const away = f.awayTeam;
          const isUser = f.homeTeamId === userId || f.awayTeamId === userId;
          const homeColor = home?.primaryColor || '#57a6ff';
          const awayColor = away?.primaryColor || '#ff647c';
          return (
            <div key={f.id} className={`fixture ${isUser ? 'user-fixture' : ''}`} style={{ opacity: f.status === 'Completed' ? 0.78 : 1 }}>
              <span>{f.status === 'Completed' ? '✓' : ''}</span>
              <span className="home" style={{ color: homeColor }}>
                {home?.shortName || 'Casa'}
              </span>
              <span className="result">{formatResult(f)}</span>
              <span className="away" style={{ color: awayColor }}>
                {away?.shortName || 'Fora'}
              </span>
            </div>
          );
        })
      )}
    </div>
  );
};

export default FixtureList;
