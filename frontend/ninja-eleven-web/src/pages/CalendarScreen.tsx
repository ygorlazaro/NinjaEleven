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
import ClubCrest from '@/components/Club/ClubCrest';
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

/**
 * A season is twenty-two days of football, and a day is what a season is made of.
 *
 * The backend does draw a real date for every matchday, and the screen does not use it. A
 * manager does not plan around the 4th of February; he plans around the fourth day, because
 * the day is what a fixture belongs to, what recovery is measured against and what the next
 * game is. Printing a real date on top of that tells him something the game does not act on,
 * and a calendar that disagrees with its own clock is one he stops believing. So the unit
 * here is the day, and the day is called what it is: Dia 4, Dia 5, and on to Dia 22.
 */
const dayOf = (matchDay: MatchDayDto | undefined): string =>
  matchDay ? `Dia ${matchDay.number}` : '—';

/** The phase inside a competition. */
const phaseOf = (
  edition: CompetitionEditionDto | undefined,
  round: RoundDto,
  ties: number
): string => {
  // A league's round number is the day itself, so a phase there would say the same thing
  // twice. A cup's round is a stage, and it is the one place the words matter: "4ª rodada"
  // tells a manager nothing about whether his club is still in the competition.
  if (!edition || edition.type === 'League') return '—';

  return cupPhaseOf(ties);
};

/**
 * What a knockout round is called, from the shape of the round itself.
 *
 * The name comes from how many ties the round holds rather than from a fixed ladder, so a
 * cup drawn with thirty-two clubs is not described with the first stage of a sixteen-club
 * one: sixteen ties is the round of thirty-two, eight is the round of sixteen, and the last
 * four are the ones everybody knows by heart. Both legs of a tie hold the same number of
 * games, so the two legs of a stage are named alike without either being told which leg it is.
 */
const cupPhaseOf = (ties: number): string => {
  if (ties >= 16) return `${ties}-avos de final`;
  if (ties === 8) return 'Oitavas de final';
  if (ties === 4) return 'Quartas de final';
  if (ties === 2) return 'Semifinal';
  if (ties === 1) return 'Final';
  return `${ties} jogos`;
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
 * The season is what the screen opens with, and the season is whole: three divisions, the
 * knockout and the super cup, in the order they are played. The competition and the club are
 * there to narrow that, not to replace it — a manager asking for "the Copa" is asking about
 * one part of a season he is also in, and a club is a filter on both ends because a manager
 * follows his own club whether it is the one with the ball or the one chasing it. Each
 * filter is optional and they compose, so "my club, in the cup" is one reading of the season
 * rather than a separate screen.
 */
const CalendarScreen: React.FC = () => {
  const navigate = useNavigate();
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [seasonId, setSeasonId] = useState<string>('');
  const [editionId, setEditionId] = useState('');
  const [teamId, setTeamId] = useState('');
  const [day, setDay] = useState('');
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

  /**
   * The season is the screen's frame, so changing it rebuilds the frame: the competition
   * filter is dropped with it, because an edition belongs to one season and an id from the
   * season just left would narrow the new one to nothing. The club filter stays — a club is
   * the same club in every season, which is the whole reason it can be remembered.
   */
  const changeSeason = (id: string) => {
    setSeasonId(id);
    setEditionId('');
  };

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

  /**
   * How many ties each round holds, which is what a knockout round is called by. Sixteen is
   * the round of thirty-two, eight is the round of sixteen, and so on down to the single tie
   * that is a final.
   */
  const tiesByRound = useMemo(() => {
    const counts: Record<string, number> = {};

    for (const fixture of fixtures) {
      counts[fixture.roundId] = (counts[fixture.roundId] ?? 0) + 1;
    }

    return counts;
  }, [fixtures]);

  /**
   * What the manager asked to see. The club is tested on both ends of the fixture, because
   * "my games" is a question about the club rather than about the end it happened to be on:
   * a filter that only knew the home side would answer with half a season and call it the
   * club's fixtures.
   */
  const shown = useMemo(
    () =>
      listed.filter(
        item =>
          (!editionId || item.round.competitionSeasonId === editionId) &&
          (!teamId || item.fixture.homeTeamId === teamId || item.fixture.awayTeamId === teamId) &&
          (!day || item.matchDay?.number === Number(day))
      ),
    [listed, editionId, teamId, day]
  );

  const played = shown.filter(item => item.fixture.status === 'Finished').length;

  /** The matchdays actually on screen, so the count follows the filters and never claims
   *  twenty-two matchdays of a cup tie that is played in two. */
  const matchDaysShown = new Set(
    shown.map(item => item.matchDay?.number).filter(number => number !== undefined)
  ).size;

  return (
    <div className="app">
      <div className="card match-header club-modal calendar-screen-card">
        <h2 className="profile-name">Calendário</h2>

        <div className="squad-toolbar">
          <label className="squad-filters">
            <span className="squad-toolbar-label">Temporada</span>
            <select value={seasonId} onChange={event => changeSeason(event.target.value)}>
              {seasons.map(season => (
                <option key={season.id} value={season.id}>
                  {season.name}
                  {season.status === 'InProgress' ? ' (em andamento)' : ''}
                </option>
              ))}
            </select>
          </label>

          <label className="squad-filters">
            <span className="squad-toolbar-label">Competição</span>
            <select value={editionId} onChange={event => setEditionId(event.target.value)}>
              <option value="">Todas</option>
              {editions.map(edition => (
                <option key={edition.id} value={edition.id}>
                  {edition.name}
                </option>
              ))}
            </select>
          </label>

          <label className="squad-filters">
            <span className="squad-toolbar-label">Clube</span>
            <select value={teamId} onChange={event => setTeamId(event.target.value)}>
              <option value="">Todos</option>
              {[...Object.values(teams)]
                .sort((a, b) => a.name.localeCompare(b.name, 'pt-BR'))
                .map(team => (
                  <option key={team.id} value={team.id}>
                    {team.name}
                  </option>
                ))}
            </select>
          </label>

          <label className="squad-filters">
            <span className="squad-toolbar-label">Dia</span>
            <select value={day} onChange={event => setDay(event.target.value)}>
              <option value="">Todos</option>
              {(calendar?.matchDays ?? [])
                .slice()
                .sort((a, b) => a.number - b.number)
                .map(matchDay => (
                  <option key={matchDay.id} value={matchDay.number}>
                    {`Dia ${matchDay.number}`}
                  </option>
                ))}
            </select>
          </label>
        </div>

        {calendar && (
          <p className="competition">
            {/* The season is named here rather than in every row: it is the frame the whole
                list sits in, and saying it four hundred times is the same noise the day
                replaced. */}
            {seasons.find(season => season.id === seasonId)?.name}
            {' • '}
            {shown.length} {shown.length === 1 ? 'jogo' : 'jogos'} • {played} disputados
            {matchDaysShown > 0 && ` • ${matchDaysShown} ${matchDaysShown === 1 ? 'dia' : 'dias'}`}
          </p>
        )}

        {error && <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>}

        <div className="calendar-list">
          {loading && shown.length === 0 ? (
            <p className="league-empty">Carregando o calendário…</p>
          ) : shown.length === 0 ? (
            <p className="league-empty">
              {listed.length === 0
                ? 'Esta temporada ainda não tem jogos marcados.'
                : 'Nenhum jogo com esses filtros.'}
            </p>
          ) : (
            <table className="history-table calendar-table">
              <thead>
                <tr>
                  <th className="calendar-day">Dia</th>
                  <th>Competição</th>
                  <th className="calendar-phase">Fase</th>
                  <th className="calendar-team">Mandante</th>
                  <th className="calendar-score">Placar</th>
                  <th className="calendar-team">Visitante</th>
                </tr>
              </thead>
              <tbody>
                {shown.map(({ fixture, round, matchDay, edition, home, away }, index) => {
                  const isManagerMatch =
                    !!selectedTeam &&
                    (fixture.homeTeamId === selectedTeam.id || fixture.awayTeamId === selectedTeam.id);

                  // A day is said once. Twenty-two rows of the championship all carry the
                  // same day, and repeating the label down the block is the noise the day
                  // was supposed to replace, so the label sits on the first row of its day
                  // and a rule is drawn instead of a second copy of the words.
                  const isFirstOfDay = shown[index - 1]?.matchDay?.id !== matchDay?.id;

                  return (
                    <tr
                      key={fixture.id}
                      className={[
                        'history-row',
                        'calendar-row',
                        isFirstOfDay ? 'opens-a-day' : '',
                        isManagerMatch ? 'is-mine' : ''
                      ]
                        .filter(Boolean)
                        .join(' ')}
                      onClick={fixture.matchId ? () => navigate(`/match/${fixture.matchId}`) : undefined}
                      title={fixture.matchId ? 'Ver a partida' : 'Ainda não disputada'}
                    >
                      <td className="calendar-day">
                        {isFirstOfDay ? dayOf(matchDay) : <span className="calendar-day__same">·</span>}
                      </td>
                      <td>
                        <span className="calendar-type">
                          {TYPE_LABEL[edition?.type ?? ''] ?? '—'}
                        </span>
                        <span className="calendar-edition">{edition?.name ?? ''}</span>
                      </td>
                      <td className="calendar-phase">
                        {phaseOf(edition, round, tiesByRound[round.id] ?? 0)}
                      </td>
                      <td className="calendar-team">
                        {home ? (
                          <span className="calendar-team__side">
                            <ClubCrest
                              primary={home.primaryColor}
                              secondary={home.secondaryColor}
                              name={home.name}
                              className="mini-crest"
                            />
                            <ClubName teamId={home.id}>{home.name}</ClubName>
                          </span>
                        ) : '…'}
                      </td>
                      <td className="calendar-score">
                        {fixture.status === 'Finished' && fixture.homeGoals !== null && fixture.homeGoals !== undefined
                          ? `${fixture.homeGoals} x ${fixture.awayGoals}`
                          : 'x'}
                      </td>
                      <td className="calendar-team">
                        {away ? (
                          <span className="calendar-team__side">
                            <ClubCrest
                              primary={away.primaryColor}
                              secondary={away.secondaryColor}
                              name={away.name}
                              className="mini-crest"
                            />
                            <ClubName teamId={away.id}>{away.name}</ClubName>
                          </span>
                        ) : '…'}
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
