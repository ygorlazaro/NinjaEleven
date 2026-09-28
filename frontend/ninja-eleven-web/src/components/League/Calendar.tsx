import React, { useEffect, useMemo, useState } from 'react';
import type { FixtureDto, RoundDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';

interface CalendarProps {
  fixtures: FixtureDto[];
  rounds: RoundDto[];
  currentRoundId?: string;
  userId?: string;
  onSelect?: (fixture: FixtureDto) => void;
}

const isPlayed = (f: FixtureDto) => f.status === 'Finished';
const isLive = (f: FixtureDto) => f.status === 'InProgress';

/**
 * The season's calendar, one matchday at a time.
 *
 * A division's season is twenty-two matchdays of six games each, and a panel that held all of
 * them at once would be a wall a manager scrolls past to find the matchday he is in. So the
 * calendar is a page per matchday, and the page it opens on is the one the manager is in — the
 * round the screen is already showing above it, chosen by the same rule that says which fixture
 * of the round he still has to play. A calendar that opened on round one would ask a manager in
 * matchday nineteen to go and find where he is.
 *
 * It follows the season: when the round that is his changes, the page changes with it, because a
 * calendar left on last week's page is a calendar answering a question about last week.
 */
const Calendar: React.FC<CalendarProps> = ({ fixtures, rounds, currentRoundId, userId, onSelect }) => {
  // Fixtures carry the round they belong to, so the calendar groups them by their real
  // round and follows the order the backend gave.
  const byRound = useMemo(() => {
    const grouped = new Map<string, FixtureDto[]>();

    fixtures.forEach(f => {
      const list = grouped.get(f.roundId) || [];
      list.push(f);
      grouped.set(f.roundId, list);
    });

    return grouped;
  }, [fixtures]);

  const ordered = useMemo(
    () => [...rounds]
      .sort((a, b) => a.number - b.number)
      .filter(round => (byRound.get(round.id) || []).length > 0),
    [rounds, byRound]
  );

  const [page, setPage] = useState(0);

  // The page the manager is on follows the round the screen is showing, and only that: turning
  // the page by hand and having the calendar snap back on the next render would be a page that
  // cannot be turned.
  useEffect(() => {
    if (!currentRoundId) return;

    const index = ordered.findIndex(round => round.id === currentRoundId);
    if (index >= 0) setPage(index);
  }, [currentRoundId, ordered]);

  // A division that has just been drawn arrives with its rounds after the first render, and a
  // page past the end of the list is a calendar showing nothing at all.
  useEffect(() => {
    setPage(current => Math.min(current, Math.max(ordered.length - 1, 0)));
  }, [ordered.length]);

  if (ordered.length === 0) {
    return <div className="league-empty">Calendário ainda não gerado.</div>;
  }

  const safePage = Math.min(page, ordered.length - 1);
  const round = ordered[safePage];
  const roundFixtures = byRound.get(round.id) || [];
  const completed = roundFixtures.every(isPlayed);
  const isCurrent = round.id === currentRoundId;

  return (
    <div id="calendarList" className="calendar-page">
      <div className={`calendar-round ${isCurrent ? 'current-round' : ''}`}>
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
                  {home ? (
                    <span className="fixture__side">
                      <ClubName teamId={f.homeTeamId}>{home.name}</ClubName>
                      <ClubCrest
                        primary={home.primaryColor || homeColor}
                        secondary={home.secondaryColor || homeColor}
                        name={home.name}
                        className="mini-crest"
                      />
                    </span>
                  ) : 'Casa'}
                </span>
                <span className="result">{result}</span>
                <span className="away" style={{ color: awayColor }}>
                  {away ? (
                    <span className="fixture__side">
                      <ClubCrest
                        primary={away.primaryColor || awayColor}
                        secondary={away.secondaryColor || awayColor}
                        name={away.name}
                        className="mini-crest"
                      />
                      <ClubName teamId={f.awayTeamId}>{away.name}</ClubName>
                    </span>
                  ) : 'Fora'}
                </span>
              </div>
            );
          })}
        </div>
      </div>

      {ordered.length > 1 && (
        <div className="pagination">
          <button
            type="button"
            className="pagination-btn"
            onClick={() => setPage(p => Math.max(0, p - 1))}
            disabled={safePage === 0}
          >
            ‹ Rodada anterior
          </button>
          <span className="pagination-info">
            Rodada {safePage + 1} de {ordered.length}
            {isCurrent ? ' (atual)' : ''} • {roundFixtures.length} jogos
          </span>
          <button
            type="button"
            className="pagination-btn"
            onClick={() => setPage(p => Math.min(ordered.length - 1, p + 1))}
            disabled={safePage >= ordered.length - 1}
          >
            Próxima rodada ›
          </button>
        </div>
      )}
    </div>
  );
};

export default Calendar;
