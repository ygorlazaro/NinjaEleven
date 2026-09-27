import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CompetitionApi, FixtureApi, SeasonApi, TeamApi } from '@/api';
import type {
  CompetitionEditionDto,
  FixtureDto,
  MatchDayDto,
  RoundDto,
  SeasonCalendarDto,
  SeasonDto,
  TeamDto
} from '@/types';
import { ClubName } from '@/components/Common/Names';
import { useGameState } from '@/state';

/** The word beside a competition, so a cup tie is not read as a league game. */
const TYPE_LABEL: Record<string, string> = {
  League: 'Campeonato',
  Cup: 'Copa',
  SuperCup: 'Supercopa'
};

interface ListedFixture {
  fixture: FixtureDto;
  round: RoundDto;
  matchDay: MatchDayDto | undefined;
  edition: CompetitionEditionDto | undefined;
  home: TeamDto | undefined;
  away: TeamDto | undefined;
}

const dateOf = (value: string): string => {
  // The date is sent as yyyy-MM-dd. Reading it as a local Date would move it a day west of
  // UTC for a manager in Brazil, which is a calendar that disagrees with itself.
  const [year, month, dayOfMonth] = value.split('-').map(Number);

  return new Date(year, month - 1, dayOfMonth).toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  });
};

/**
 * A season's football, in the order it was and will be played.
 *
 * The order is the season's, not the screen's: every fixture belongs to a window, every
 * window belongs to a matchday, and the matchday is what has a date. Sorting by a window's
 * own number would put the cup's third round next to the league's third round as though they
 * happened together, because each competition counts its rounds from one and neither knows
 * about the other. A calendar that cannot say the day is a list.
 *
 * Only the season is a filter. A manager reading a calendar wants his season whole — the
 * three divisions and the knockout he is also in — and narrowing it by competition would be
 * a question about which competition, which is the table's question and not this screen's.
 */
const CalendarScreen: React.FC = () => {
  const navigate = useNavigate();
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [seasonId, setSeasonId] = useState<string>('');
  const [calendar, setCalendar] = useState<SeasonCalendarDto | null>(null);
  const [editions, setEditions] = useState<CompetitionEditionDto[]>([]);
  const [fixtures, setFixtures] = useState<FixtureDto[]>([]);
  const [teams, setTeams] = useState<Record<string, TeamDto>>({});
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const seasonList = await SeasonApi.list();
        if (cancelled) return;

        // Newest first: a manager opening a calendar is asking about the season in progress.
        const ordered = [...seasonList].sort((a, b) => b.number - a.number);
        const current = ordered.find(season => season.status === 'InProgress') ?? ordered[0];

        setSeasons(ordered);
        if (current) setSeasonId(current.id);
      } catch (err) {
        console.error('Failed to load the seasons:', err);
        if (!cancelled) setError('Não foi possível carregar as temporadas.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!seasonId) return undefined;

    let cancelled = false;

    const load = async () => {
      setLoading(true);

      try {
        // The calendar asks for the draw with build, so a season that has never been drawn
        // is drawn by the act of reading it. The teams come once for every season, since a
        // club does not change identity between them.
        const [drawn, editionList, allFixtures, teamList] = await Promise.all([
          SeasonApi.getCalendar(seasonId),
          CompetitionApi.listEditionsBySeason(seasonId),
          FixtureApi.list(),
          TeamApi.list()
        ]);

        if (cancelled) return;

        setCalendar(drawn);
        setEditions(editionList);
        setFixtures(allFixtures);
        setTeams(Object.fromEntries(teamList.map(team => [team.id, team])));
      } catch (err) {
        console.error('Failed to load the calendar:', err);
        if (!cancelled) setError('Não foi possível carregar o calendário.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [seasonId]);

  const listed = useMemo<ListedFixture[]>(() => {
    if (!calendar) return [];

    const matchDaysById = Object.fromEntries(calendar.matchDays.map(day => [day.id, day]));
    const roundsById = Object.fromEntries(calendar.windows.map(round => [round.id, round]));
    const editionsById = Object.fromEntries(editions.map(edition => [edition.id, edition]));

    return fixtures
      .filter(fixture => roundsById[fixture.roundId])
      .map(fixture => {
        const round = roundsById[fixture.roundId];
        const matchDay = round.matchDayId ? matchDaysById[round.matchDayId] : undefined;

        return {
          fixture,
          round,
          matchDay,
          edition: editionsById[round.competitionSeasonId],
          home: teams[fixture.homeTeamId],
          away: teams[fixture.awayTeamId]
        };
      })
      .sort((a, b) => {
        // The day first, then the window in it: the championship is played before the cup
        // on the same afternoon, and the reverse would read as the cup being drawn on
        // Tuesday's football. The fixture id settles a tie so the order never jitters.
        const dayA = a.matchDay?.number ?? Number.MAX_SAFE_INTEGER;
        const dayB = b.matchDay?.number ?? Number.MAX_SAFE_INTEGER;

        return dayA - dayB
          || a.round.window - b.round.window
          || a.fixture.id.localeCompare(b.fixture.id);
      });
  }, [calendar, editions, fixtures, teams]);

  const played = listed.filter(item => item.fixture.status === 'Finished').length;

  return (
    <div className="app">
      <div className="card match-header club-modal">
        <h2 className="profile-name">Calendário</h2>

        <label className="squad-filters">
          <span className="squad-toolbar-label">Temporada</span>
          <select value={seasonId} onChange={event => setSeasonId(event.target.value)}>
            {seasons.map(season => (
              <option key={season.id} value={season.id}>
                {season.name}
                {season.status === 'InProgress' ? ' (em andamento)' : ''}
              </option>
            ))}
          </select>
        </label>

        {calendar && (
          <p className="competition">
            {listed.length} jogos • {played} disputados
            {calendar.matchDayCount > 0 && ` • ${calendar.matchDayCount} rodadas`}
          </p>
        )}

        {error && <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>}

        <div className="calendar-list">
          {loading && listed.length === 0 ? (
            <p className="league-empty">Carregando o calendário…</p>
          ) : listed.length === 0 ? (
            <p className="league-empty">Esta temporada ainda não tem jogos marcados.</p>
          ) : (
            <table className="history-table calendar-table">
              <thead>
                <tr>
                  <th className="num">Rodada</th>
                  <th>Data</th>
                  <th>Competição</th>
                  <th className="num">Fase</th>
                  <th className="calendar-team">Mandante</th>
                  <th className="calendar-score">Placar</th>
                  <th className="calendar-team">Visitante</th>
                </tr>
              </thead>
              <tbody>
                {listed.map(({ fixture, round, matchDay, edition, home, away }) => {
                  const isManagerMatch =
                    !!selectedTeam &&
                    (fixture.homeTeamId === selectedTeam.id || fixture.awayTeamId === selectedTeam.id);

                  return (
                    <tr
                      key={fixture.id}
                      className={`history-row calendar-row ${isManagerMatch ? 'is-mine' : ''}`}
                      onClick={fixture.matchId ? () => navigate(`/match/${fixture.matchId}`) : undefined}
                      title={fixture.matchId ? 'Ver a partida' : 'Ainda não disputada'}
                    >
                      <td className="num">{matchDay?.number ?? '—'}</td>
                      <td className="calendar-date">{matchDay ? dateOf(matchDay.date) : '—'}</td>
                      <td>
                        <span className="calendar-type">
                          {TYPE_LABEL[edition?.type ?? ''] ?? '—'}
                        </span>
                        <span className="calendar-edition">{edition?.name ?? ''}</span>
                      </td>
                      <td className="num">{round.number}ª</td>
                      <td className="calendar-team">
                        {home ? <ClubName teamId={home.id}>{home.name}</ClubName> : '…'}
                      </td>
                      <td className="calendar-score">
                        {fixture.status === 'Finished' && fixture.homeGoals !== null && fixture.homeGoals !== undefined
                          ? `${fixture.homeGoals} x ${fixture.awayGoals}`
                          : 'x'}
                      </td>
                      <td className="calendar-team">
                        {away ? <ClubName teamId={away.id}>{away.name}</ClubName> : '…'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
};

export default CalendarScreen;
