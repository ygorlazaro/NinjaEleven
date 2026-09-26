import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { CompetitionApi, FixtureApi, LeagueApi, RoundApi, SeasonApi, TeamApi } from '@/api';
import { useGameState } from '@/state';
import type { FixtureDto, LeagueSetupResult, RoundDto, TeamDto } from '@/types';
import StandingsTable from '@/components/League/StandingsTable';
import FixtureList from '@/components/League/FixtureList';
import Calendar from '@/components/League/Calendar';
import ScorersList from '@/components/League/ScorersList';

const isScheduled = (f: FixtureDto) => f.status === 'Scheduled';
const involves = (f: FixtureDto, teamId?: string) =>
  !!teamId && (f.homeTeamId === teamId || f.awayTeamId === teamId);

const LeagueScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const leagueTeams = useGameState((s) => s.leagueTeams);
  const standings = useGameState((s) => s.standings);
  const scorers = useGameState((s) => s.scorers);
  const leagueSetup = useGameState((s) => s.leagueSetup);
  const setStandings = useGameState((s) => s.setStandings);
  const setScorers = useGameState((s) => s.setScorers);
  const setLeagueSetup = useGameState((s) => s.setLeagueSetup);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const forgetClub = useGameState((s) => s.forgetClub);

  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [rounds, setRounds] = useState<RoundDto[]>([]);
  const [allFixtures, setAllFixtures] = useState<FixtureDto[]>([]);
  const [roundFixtures, setRoundFixtures] = useState<FixtureDto[]>([]);
  const [currentRoundId, setCurrentRoundId] = useState('');
  const [compSeasonId, setCompSeasonId] = useState('');
  const [error, setError] = useState<string | null>(null);

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
    async (competitionSeasonId: string, seasonId: string) => {
      const [fixtureData, standingData, scorerData] = await Promise.all([
        FixtureApi.list(),
        LeagueApi.getStandings(competitionSeasonId),
        LeagueApi.getScorers(seasonId),
      ]);

      setAllFixtures(fixtureData);
      setStandings(standingData);
      setScorers(scorerData);
    },
    [setStandings, setScorers]
  );

  useEffect(() => {
    const season = params.get('season') || '';
    const competition = params.get('competition') || '';

    const initialize = async () => {
      setError(null);

      try {
        // The persisted career may not have the club list yet (first visit after a
        // reload), so the teams always come from the API and are stored afterwards.
        const seasonId = season || (await SeasonApi.current()).id;
        const competitionId = competition || (await CompetitionApi.listBySeason(seasonId))[0]?.id;

        if (!competitionId) {
          setError('Nenhuma competição disponível para a temporada escolhida.');
          return;
        }

        const teams: TeamDto[] = await TeamApi.list();
        if (teams.length < 2) {
          setError('O campeonato precisa de pelo menos dois clubes.');
          return;
        }
        setLeagueTeams(teams);

        const setup: LeagueSetupResult = await LeagueApi.setup(
          competitionId,
          seasonId,
          teams.map(t => t.id)
        );
        setLeagueSetup(setup);

        const id = setup.competitionSeasonId || '';
        setCompSeasonId(id);

        const roundList = await RoundApi.listByCompetitionSeason(id);
        setRounds(roundList);

        await refresh(id, seasonId);

        const fixtureData = await FixtureApi.list();
        setCurrentRoundId(pickCurrentRound(roundList, fixtureData));
      } catch (err: any) {
        const code = err?.response?.data?.code;
        setError(code ? `${code}: ${err.response.data.detail}` : 'Não foi possível carregar o campeonato.');
      }
    };

    initialize();
  }, [params, pickCurrentRound, refresh, setLeagueSetup, setLeagueTeams]);

  useEffect(() => {
    if (!currentRoundId) {
      setRoundFixtures([]);
      return;
    }
    setRoundFixtures(allFixtures.filter(f => f.roundId === currentRoundId));
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
    navigate(`/match/lineup/${userFixture.id}`);
  };

  const openFixture = (fixture: FixtureDto) => {
    if (fixture.matchId) {
      navigate(`/match/${fixture.matchId}`);
      return;
    }

    if (isScheduled(fixture) && involves(fixture, selectedTeam?.id)) {
      navigate(`/match/lineup/${fixture.id}`);
    }
  };

  const changeClub = () => {
    forgetClub();
    navigate('/');
  };

  const otherResults = roundFixtures.filter(
    f => f.status === 'Finished' && !involves(f, selectedTeam?.id)
  );

  return (
    <div className="card league-screen">
      <div className="league-head">
        <div>
          <h2>Campeonato</h2>
          <div className="badge" style={{ display: 'inline-block', marginTop: '7px' }}>
            {currentRound ? `Rodada ${currentRound.number}/${totalRounds}` : 'Sem rodada'}
          </div>
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          {selectedTeam && (
            <button className="ctrl" onClick={changeClub}>
              Trocar de clube
            </button>
          )}
          <button className="primary" disabled={!userFixture} onClick={openLineup}>
            ⚽ Escalação e partida
          </button>
        </div>
      </div>

      {error && (
        <p className="competition" style={{ color: 'var(--danger)', margin: '0 0 10px' }}>{error}</p>
      )}

      <div className="league-grid">
        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className="league-panel">
            <h3>📊 Classificação</h3>
            <div id="standingsWrap">
              <StandingsTable standings={standings} userId={selectedTeam?.id} teams={leagueTeams} />
            </div>
          </div>

          <div className="league-panel">
            <h3>Jogos da rodada {currentRound?.number ?? ''}</h3>
            <div id="roundFixtures">
              <FixtureList
                fixtures={roundFixtures}
                userId={selectedTeam?.id}
                onSelect={openFixture}
              />
            </div>
            {!userFixture && roundFixtures.length > 0 && (
              <p className="squad-hint" style={{ marginTop: '8px' }}>
                {otherResults.length < roundFixtures.length
                  ? 'Seu clube já jogou nesta rodada. As outras partidas estão em andamento.'
                  : 'Nenhum jogo seu pendente nesta rodada.'}
              </p>
            )}
          </div>

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
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className="league-panel">
            <h3>⚽ Outros resultados</h3>
            <div id="otherResults">
              {otherResults.length === 0 ? (
                <div className="league-note">Ainda não há resultados de outras partidas.</div>
              ) : (
                <FixtureList
                  fixtures={otherResults}
                  userId={selectedTeam?.id}
                  onSelect={openFixture}
                />
              )}
            </div>
          </div>

          <div className="league-panel">
            <h3>🥅 Artilheiros — Top 15</h3>
            <div id="scorersWrap">
              <ScorersList scorers={scorers} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default LeagueScreen;
