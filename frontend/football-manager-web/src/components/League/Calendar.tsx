import React from 'react';
import type { FixtureDto } from '@/types';

interface CalendarProps {
  fixtures: FixtureDto[];
  currentRound: number;
  userId?: string;
}

const Calendar: React.FC<CalendarProps> = ({ fixtures, currentRound, userId }) => {
  const rounds: FixtureDto[][] = [];
  fixtures.forEach(f => {
    const roundNum = Math.floor(Math.random() * 14);
    if (!rounds[roundNum]) rounds[roundNum] = [];
    rounds[roundNum].push(f);
  });

  return (
    <div id="calendarList" style={{ display: 'flex', flexDirection: 'column', gap: '8px', maxHeight: 'none', overflow: 'visible', paddingRight: '4px' }}>
      {rounds.map((roundFixtures, i) => {
        if (!roundFixtures || roundFixtures.length === 0) return null;
        const completed = roundFixtures.length === 4 && roundFixtures.every(f => f.status === 'Completed');
        const isCurrent = i === currentRound;
        return (
          <div key={i} className={`calendar-round ${isCurrent ? 'current-round' : ''}`}>
            <div className="calendar-round-title">
              Rodada {i + 1}
              {isCurrent ? ' • ATUAL' : ''}
              {completed ? ' • CONCLUÍDA' : ''}
            </div>
            <div className="calendar-round-body">
              {roundFixtures.map(f => {
                const home = f.homeTeam;
                const away = f.awayTeam;
                const u = userId;
                const isUserFixture = f.homeTeamId === u || f.awayTeamId === u;
                const homeColor = home?.primaryColor || '#57a6ff';
                const awayColor = away?.primaryColor || '#ff647c';
                const result = f.homeGoals !== null && f.homeGoals !== undefined
                  ? `${f.homeGoals} × ${f.awayGoals}`
                  : '—';
                return (
                  <div
                    key={f.id}
                    className={`fixture calendar-fixture ${f.status === 'Completed' ? 'played' : ''} ${isUserFixture ? 'user-fixture' : ''}`}
                  >
                    <span>{f.status === 'Completed' ? '✓' : ''}</span>
                    <span className="home" style={{ color: homeColor }}>
                      {home?.shortName || 'Casa'}
                    </span>
                    <span className="result">{result}</span>
                    <span className="away" style={{ color: awayColor }}>
                      {away?.shortName || 'Fora'}
                    </span>
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
};

export default Calendar;
