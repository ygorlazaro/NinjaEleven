import React from 'react';
import type { FixtureDto, RoundDto } from '@/types';

interface CalendarProps {
  fixtures: FixtureDto[];
  rounds: RoundDto[];
  currentRoundId?: string;
  userId?: string;
  onSelect?: (fixture: FixtureDto) => void;
}

const isPlayed = (f: FixtureDto) => f.status === 'Finished';
const isLive = (f: FixtureDto) => f.status === 'InProgress';

const Calendar: React.FC<CalendarProps> = ({ fixtures, rounds, currentRoundId, userId, onSelect }) => {
  // Fixtures carry the round they belong to, so the calendar groups them by their real
  // round and follows the order the backend gave.
  const byRound = new Map<string, FixtureDto[]>();
  fixtures.forEach(f => {
    const list = byRound.get(f.roundId) || [];
    list.push(f);
    byRound.set(f.roundId, list);
  });

  const ordered = [...rounds]
    .sort((a, b) => a.number - b.number)
    .filter(round => (byRound.get(round.id) || []).length > 0);

  return (
    <div id="calendarList" style={{ display: 'flex', flexDirection: 'column', gap: '8px', maxHeight: 'none', overflow: 'visible', paddingRight: '4px' }}>
      {ordered.length === 0 && <div className="league-empty">Calendário ainda não gerado.</div>}

      {ordered.map(round => {
        const roundFixtures = byRound.get(round.id) || [];
        const completed = roundFixtures.every(isPlayed);
        const isCurrent = round.id === currentRoundId;

        return (
          <div key={round.id} className={`calendar-round ${isCurrent ? 'current-round' : ''}`}>
            <div className="calendar-round-title">
              Rodada {round.number}
              {isCurrent ? ' • ATUAL' : ''}
              {completed ? ' • CONCLUÍDA' : ''}
            </div>
            <div className="calendar-round-body">
              {roundFixtures.map(f => {
                const home = f.homeTeam;
                const away = f.awayTeam;
                const isUserFixture = f.homeTeamId === userId || f.awayTeamId === userId;
                const homeColor = home?.primaryColor || '#57a6ff';
                const awayColor = away?.primaryColor || '#ff647c';
                const result =
                  f.homeGoals !== null && f.homeGoals !== undefined
                    ? `${f.homeGoals} × ${f.awayGoals}`
                    : '—';

                // Same rule as the round list: a fixture with a match is watched, a
                // scheduled fixture of the manager is entered through the lineup.
                const link = f.matchId
                  ? `/match/${f.matchId}`
                  : f.status === 'Scheduled' && isUserFixture
                    ? `/match/lineup/${f.id}`
                    : null;

                return (
                  <div
                    key={f.id}
                    className={`fixture calendar-fixture ${isPlayed(f) ? 'played' : ''} ${isLive(f) ? 'live' : ''} ${isUserFixture ? 'user-fixture' : ''} ${link ? 'clickable' : ''}`}
                    onClick={link && onSelect ? () => onSelect(f) : undefined}
                  >
                    <span>{isPlayed(f) ? '✓' : isLive(f) ? '●' : ''}</span>
                    <span className="home" style={{ color: homeColor }}>
                      {home?.name || 'Casa'}
                    </span>
                    <span className="result">{result}</span>
                    <span className="away" style={{ color: awayColor }}>
                      {away?.name || 'Fora'}
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
