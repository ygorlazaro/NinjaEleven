import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { CompetitionApi, FixtureApi, LeagueApi, MatchApi, RoundApi, SeasonApi, TeamApi } from '@/api';
import { useGameState } from '@/state';
import type {
  CompetitionEditionDto,
  DivisionPurseDto,
  FixtureDto,
  MatchdayReportDto,
  RoundDto,
  SeasonDto,
  TeamDto,
  TopScorerPrizeListDto
} from '@/types';
import { divisionsOf } from '@/types';
import StandingsTable from '@/components/League/StandingsTable';
import Calendar from '@/components/League/Calendar';
import ScorersList from '@/components/League/ScorersList';
import MatchdayReportPanel from '@/components/League/MatchdayReportPanel';
import PrizeLegend from '@/components/League/PrizeLegend';
import DivisionTrophy from '@/components/League/DivisionTrophy';
import TopScorerPrizePanel from '@/components/League/TopScorerPrizePanel';

const isScheduled = (f: FixtureDto) => f.status === 'Scheduled';
const involves = (f: FixtureDto, teamId?: string) =>
  !!teamId && (f.homeTeamId === teamId || f.awayTeamId === teamId);

const LeagueScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const leagueTeams = useGameState((s) => s.leagueTeams);
  const standings = useGameState((s) => s.standings);
  const scorers = useGameState((s) => s.scorers);
  const setStandings = useGameState((s) => s.setStandings);
  const setScorers = useGameState((s) => s.setScorers);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const selectedCompetition = useGameState((s) => s.selectedCompetition);
  const setCompetitionInfo = useGameState((s) => s.setCompetitionInfo);
  const forgetClub = useGameState((s) => s.forgetClub);

  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [rounds, setRounds] = useState<RoundDto[]>([]);
  const [allFixtures, setAllFixtures] = useState<FixtureDto[]>([]);
  const [roundFixtures, setRoundFixtures] = useState<FixtureDto[]>([]);
  const [currentRoundId, setCurrentRoundId] = useState('');
  const [compSeasonId, setCompSeasonId] = useState('');
  const [editions, setEditions] = useState<CompetitionEditionDto[]>([]);
  // Seasons drive the divisions on top, so the list is read once: the dropdown lets a
  // manager visit a past table, and the default is always the season in progress.
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [seasonsLoading, setSeasonsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [report, setReport] = useState<MatchdayReportDto | null>(null);
  // What each division's table is paid out of. The pyramid's money is a rule of the game rather
  // than a fact about this season, so it is asked for once and it does not change with the
  // filters: a manager reading his ninth place wants to know what ninth place is worth.
  const [purses, setPurses] = useState<DivisionPurseDto[]>([]);
  // The artilharia of the division on screen, which is not the same number everywhere: each
  // division's three top scorers are paid a share of that division's own title, so the panel
  // follows the dropdown rather than being read once for the pyramid.
  const [scorerPrize, setScorerPrize] = useState<TopScorerPrizeListDto | null>(null);

  // The round the manager is in is the first one that still has a fixture nobody
  // played. Rounds are not gated by the calendar: they simply follow the results.
  const pickCurrentRound = useCallback((roundList: RoundDto[], fixtureList: FixtureDto[]) => {
    const ordered = [...roundList].sort((a, b) => a.number - b.number);
    const firstOpen = ordered.find(round =>
      fixtureList.some(f => f.roundId === round.id && isScheduled(f))
    );
    return (firstOpen || ordered[ordered.length - 1])?.id || '';
  }, []);

  const refresh = useCallback(
    async (
      competitionSeasonId: string,
      seasonId: string,
      fixtureData: FixtureDto[],
      divisionId?: string | null,
    ) => {
      // The artilharia is asked of the division being shown, so the money under the table is the
      // money of that table. A prize panel that did not follow the dropdown would be paying a
      // third-division striker a first-division cheque on the first division's page — and so
      // would a chart that did not follow it, which is the same mistake told with goals instead
      // of money: the league is four divisions, so a chart asked of "the league" alone is a
      // chart of the country and the top line on it is nobody in this division.
      //
      // The fixture list arrives as an argument because it is read once by the caller and used
      // twice: the calendar and the round that is being looked at are the same rows, and a
      // screen that fetched them a second time was paying for the season's whole football twice
      // on every load.
      const [standingData, scorerData, prizeData] = await Promise.all([
        LeagueApi.getStandings(competitionSeasonId),
        LeagueApi.getScorers(seasonId, undefined, undefined, divisionId ?? undefined),
        CompetitionApi.getTopScorerPrizes(competitionSeasonId).catch(() => null),
      ]);

      setStandings(standingData);
      setScorers(scorerData);
      setScorerPrize(prizeData);
      setAllFixtures(fixtureData);
    },
    [setStandings, setScorers]
  );

  // Which division the screen is showing, and which season it belongs to.
  //
  // The division is the manager's choice, and it is the screen's whole subject: a pyramid of
  // three divisions has three tables, and one of them is chosen. It travels in the query
  // string, which is what makes a table somebody is looking at a link they can send on.
  // The pyramid's money does not belong to a season or a division, so it is read once when the
  // screen opens rather than on every filter change.
  useEffect(() => {
    let alive = true;

    LeagueApi.getPrizes()
      .then(list => alive && setPurses(list))
      .catch(() => alive && setPurses([]));

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      setError(null);

      try {
        const seasonId = params.get('season') || (await SeasonApi.current()).id;
        const editionList = await CompetitionApi.listEditionsBySeason(seasonId);

        if (cancelled) return;

        const divisions = editionList.filter(edition => edition.isDivision);
        const wanted = params.get('edition') || '';

        // The division a manager opens on is his club's own. A pyramid of four divisions has
        // four tables and the manager has one of them: sending him to the first division to
        // read a table his club is not in is a screen that opens on somebody else's season. So
        // the club is asked where it is, and its division is the default — while an explicit
        // `?edition=` in the link still wins, because a table somebody is looking at is a link
        // they can send on.
        const clubStanding = selectedTeam
          ? await TeamApi.getStanding(selectedTeam.id, seasonId).catch(() => null)
          : null;

        if (cancelled) return;

        const division =
          divisions.find(edition => edition.id === wanted) ||
          divisions.find(edition => edition.id === clubStanding?.competitionSeasonId) ||
          divisions.find(edition => edition.id === selectedCompetition?.id) ||
          divisions[0];

        if (!division) {
          setError('Nenhuma divisão disponível para a temporada escolhida.');
          return;
        }

        setEditions(divisions);
        setCompSeasonId(division.id);
        setCompetitionInfo(division);

        // The clubs of this division, and only this division's: they are the ones the table
        // is about and the ones the calendar below belongs to. The season's fixtures come in
        // the same breath — the calendar is the season, and it is one read of the world.
        const [clubs, roundList, fixtureData] = await Promise.all([
          CompetitionApi.listClubs(division.id),
          RoundApi.listByCompetitionSeason(division.id),
          FixtureApi.list(),
        ]);
        if (cancelled) return;
        setLeagueTeams(clubs);

        // The calendar was drawn with the season, so it is read rather than built. Asking for
        // it to be set up again would draw a second schedule for clubs that already have one,
        // and with thirty-six clubs in one edition it would draw the wrong one: the pyramid
        // gives each division its own twelve, and that is not something a client can hand in.
        setRounds(roundList);
        setCurrentRoundId(pickCurrentRound(roundList, fixtureData));

        // The division comes with it, because the artilharia is that division's and not the
        // country's. The screen knows which edition it is showing; the chart is asked of that
        // edition's division rather than of "the league", which is four of them.
        await refresh(division.id, seasonId, fixtureData, division.divisionId);
      } catch (err: any) {
        const code = err?.response?.data?.code;
        if (!cancelled) {
          setError(code ? `${code}: ${err.response.data.detail}` : 'Não foi possível carregar o campeonato.');
        }
      }
    };

    initialize();

    return () => { cancelled = true; };
  }, [params, pickCurrentRound, refresh, setCompetitionInfo, setLeagueTeams, selectedTeam?.id]);

  // The season list is independent of the chosen division, so it is read once. The dropdown
  // reads it; the default the screen falls back to is the season that is in progress, exactly
  // the one `SeasonApi.current` would answer — the dropdown just lets a manager look back.
  useEffect(() => {
    let cancelled = false;
    setSeasonsLoading(true);

    SeasonApi.list()
      .then(loaded => {
        if (cancelled) return;
        setSeasons(loaded);
      })
      .catch(() => { if (!cancelled) setSeasons([]); })
      .finally(() => { if (!cancelled) setSeasonsLoading(false); });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!currentRoundId) {
      setRoundFixtures([]);
      return;
    }
    setRoundFixtures(allFixtures.filter(f => f.roundId === currentRoundId));
  }, [currentRoundId, allFixtures]);

  // The round, told back. Asked of the backend rather than assembled here: the summary is
  // the goals' own narration read out of the events, and a screen that rebuilt it from the
  // scoreline would be writing a second account of a match that was only ever told once.
  useEffect(() => {
    if (!currentRoundId) {
      setReport(null);
      return;
    }

    let cancelled = false;

    MatchApi.getRoundReport(currentRoundId)
      .then(loaded => {
        if (!cancelled) setReport(loaded);
      })
      .catch(err => {
        console.error('Failed to load the matchday report:', err);
        if (!cancelled) setReport(null);
      });

    return () => {
      cancelled = true;
    };
  }, [currentRoundId, allFixtures]);

  const currentRound = rounds.find(r => r.id === currentRoundId);
  const totalRounds = rounds.length;

  // The fixture the manager has to play: the one of the current round that is still
  // waiting. A fixture that is being played or already finished is not offered again —
  // it is watched from the fixture list instead.
  const userFixture = roundFixtures.find(
    f => involves(f, selectedTeam?.id) && isScheduled(f)
  ) || null;

  const openLineup = () => {
    if (!userFixture) return;
    navigate('/tactics');
  };

  const openFixture = (fixture: FixtureDto) => {
    if (fixture.matchId) {
      navigate(`/match/${fixture.matchId}`);
      return;
    }

    if (isScheduled(fixture) && involves(fixture, selectedTeam?.id)) {
      navigate('/tactics');
    }
  };

  const changeClub = () => {
    forgetClub();
    navigate('/');
  };

  // Switching division keeps the season and drops the division, so the season the manager is
  // in is not something they have to choose again every time they look at another table.
  const changeDivision = (editionId: string) => {
    const season = params.get('season') || '';
    setCompSeasonId('');
    setCurrentRoundId('');
    setRounds([]);
    setAllFixtures([]);
    navigate(`/league?season=${season}&edition=${editionId}`);
  };

  // Editions are owned by a season, so changing the season drops the division and lets the
  // screen pick the first division of the new one. The season itself travels in the query
  // string so a table a manager is looking at is a link they can send on.
  const changeSeason = (seasonId: string) => {
    if (!seasonId) return;
    setCompSeasonId('');
    setEditions([]);
    setCurrentRoundId('');
    setRounds([]);
    setAllFixtures([]);
    navigate(`/league?season=${seasonId}`);
  };

  const currentSeason = seasons.find(season => season.status === 'InProgress');
  // The dropdown defaults to the current season when the manager has not chosen one.
  const seasonValue = params.get('season') || currentSeason?.id || '';

  const activeDivision = editions.find(edition => edition.id === compSeasonId);

  return (
    <div className="card league-screen">
      <div className="league-head">
        <div>
          {/* The division's own cup beside its name. Three divisions are three different
              competitions with three different purses, and the biggest text on the screen is
              where a manager reads which one he is in — so the mark that says it is the
              division's, drawn to the shape of its rank rather than a trophy of no particular
              division. */}
          <h2 className="league-screen__division">
            {activeDivision?.tier != null && <DivisionTrophy tier={activeDivision.tier} size={26} />}
            <span>{activeDivision?.name ?? 'Campeonato'}</span>
          </h2>
          <div className="badge" style={{ display: 'inline-block', marginTop: '7px' }}>
            {currentRound ? `Rodada ${currentRound.number}/${totalRounds}` : 'Sem rodada'}
          </div>
          {seasons.length > 1 && (
            <div style={{ marginTop: '8px' }}>
              <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Temporada:</label>{' '}
              <select
                value={seasonValue}
                onChange={e => changeSeason(e.target.value)}
                disabled={seasonsLoading}
                aria-label="Temporada"
                style={{
                  padding: '4px 6px',
                  background: '#0a1520',
                  color: 'var(--text)',
                  border: '1px solid var(--line)',
                  borderRadius: '6px',
                  fontSize: '14px'
                }}
              >
                {seasons
                  .slice()
                  .sort((a, b) => b.number - a.number)
                  .map(season => (
                    <option key={season.id} value={season.id}>{season.name}</option>
                  ))}
              </select>
            </div>
          )}
          {editions.length > 1 && (
            <div style={{ marginTop: '8px' }}>
              <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Divisão:</label>{' '}
              <select
                value={compSeasonId}
                onChange={e => changeDivision(e.target.value)}
                aria-label="Divisão"
                style={{
                  padding: '4px 6px',
                  background: '#0a1520',
                  color: 'var(--text)',
                  border: '1px solid var(--line)',
                  borderRadius: '6px',
                  fontSize: '14px'
                }}
              >
                {editions.map(edition => (
                  <option key={edition.id} value={edition.id}>{edition.name}</option>
                ))}
              </select>
            </div>
          )}
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          {selectedTeam && (
            <>
              <button
                className="ctrl"
                onClick={() => navigate(
                  `/team/${selectedTeam.id}?season=${params.get('season') || ''}&edition=${compSeasonId}`
                )}
              >
                Meu elenco
              </button>
              <button className="ctrl" onClick={changeClub}>
                Trocar de clube
              </button>
            </>
          )}
          <button className="primary" disabled={!userFixture} onClick={openLineup}>
            ⚽ Escalação e partida
          </button>
        </div>
      </div>

      {error && (
        <p className="competition" style={{ color: 'var(--danger)', margin: '0 0 10px' }}>{error}</p>
      )}

      {/* The table is the screen and the calendar is beside it. A division's table is the thing
          a manager opens the page for, so it takes the wide column and the artilharia sits under
          it where the eye already is; the calendar is the other way round the world — a season
          read a matchday at a time, and it is the one panel that is looked at on its own. */}
      <div className="league-grid">
        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className="league-panel">
            <h3>📊 Classificação</h3>
            <div id="standingsWrap">
              <StandingsTable
                standings={standings?.official ?? []}
                userId={selectedTeam?.id}
                teams={leagueTeams}
                tier={activeDivision?.tier}
                lastTier={divisionsOf(editions).length}
              />
            </div>
            <MatchdayReportPanel
              report={report}
              userTeamId={selectedTeam?.id}
              onSelect={matchId => navigate(`/match/${matchId}`)}
            />
          </div>

          <div className="league-panel">
            {/* The division is named in the title because the chart below it is that
                division's and not the country's: a manager reading the 3ª Divisão's table with
                a heading that says only "Artilheiros" cannot tell a list of his own division
                from a list of everybody's, and a chart that could be either is a chart he has
                to check against something else before he trusts a name on it. */}
            <h3>
              🥅 Artilheiros
              {activeDivision?.name ? ` — ${activeDivision.name}` : ''}
            </h3>
            <div id="scorersWrap">
              {/* Ten names: the artilharia of a championship is a top ten a manager reads whole,
                  and a table of fifteen is the season's list rather than its chart. */}
              <ScorersList
                scorers={scorers}
                userTeamId={selectedTeam?.id}
                userTeamName={selectedTeam?.name}
                allLabel={activeDivision?.name ?? 'Campeonato'}
                limit={10}
              />
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className="league-panel">
            <h3>📅 Calendário completo</h3>
            <Calendar
              fixtures={allFixtures}
              rounds={rounds}
              currentRoundId={currentRoundId}
              userId={selectedTeam?.id}
              onSelect={openFixture}
            />
          </div>

          <div className="league-panel">
            <TopScorerPrizePanel prize={scorerPrize} />
          </div>

          <div className="league-panel">
            <PrizeLegend purses={purses} tier={activeDivision?.tier} />
          </div>
        </div>
      </div>
    </div>
  );
};

export default LeagueScreen;
