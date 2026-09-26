import React, { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { CompetitionApi, LeagueApi, SeasonApi } from '@/api';
import { useGameState } from '@/state';
import type { FixtureDto, StandingDto, ScorerDto, LeagueSetupResult } from '@/types';
import StandingsTable from '@/components/League/StandingsTable';
import FixtureList from '@/components/League/FixtureList';
import Calendar from '@/components/League/Calendar';
import ScorersList from '@/components/League/ScorersList';
import { useLeagueSetup, useMatchEngine } from '@/services';

const LeagueScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const leagueTeams = useGameState((s) => s.leagueTeams);
  const standings = useGameState((s) => s.standings);
  const fixtures = useGameState((s) => s.fixtures);
  const scorers = useGameState((s) => s.scorers);
  const leagueSetup = useGameState((s) => s.leagueSetup);
  const setStandings = useGameState((s) => s.setStandings);
  const setFixtures = useGameState((s) => s.setFixtures);
  const setScorers = useGameState((s) => s.setScorers);
  const setLeagueSetup = useGameState((s) => s.setLeagueSetup);

  const [compSeasonId, setCompSeasonId] = useState<string>('');
  const navigate = useNavigate();
  const [currentRound, setCurrentRound] = useState(0);
  const [lastCompletedRound, setLastCompletedRound] = useState(-1);
  const [finished, setFinished] = useState(false);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const season = params.get('season') || '';
    const competition = params.get('competition') || '';

    const initialize = async () => {
      if (leagueTeams.length < 2) return;

      try {
        // The query string may be missing or stale (deep link, refresh), so the season
        // and the competition are resolved from the API before setting the league up.
        const seasonId = season || (await SeasonApi.current()).id;
        const competitionId = competition
          || (await CompetitionApi.listBySeason(seasonId))[0]?.id;

        if (!competitionId) {
          console.error('No competition available for the selected season.');
          return;
        }

        const setup = await LeagueApi.setup(
          competitionId,
          seasonId,
          leagueTeams.map(t => t.id)
        );
        setLeagueSetup(setup);
        setCompSeasonId(setup.competitionSeasonId || '');

        const fixturesData = setup.fixtures || [];
        setFixtures(fixturesData.slice(0, 4));
        setCurrentRound(0);

        const standingsData = await LeagueApi.getStandings(setup.competitionSeasonId || '');
        setStandings(standingsData);

        const scorersData = await LeagueApi.getScorers(seasonId);
        setScorers(scorersData);
      } catch (error) {
        console.error('Failed to initialize league:', error);
      }
    };

    initialize();
  }, [leagueTeams]);

  const fixtureForUser = useCallback(() => {
    if (!leagueSetup?.fixtures) return null;
    const uid = selectedTeam?.id;
    if (!uid) return null;

    const allFixtures = leagueSetup?.fixtures || [];
    const playedFixtures = allFixtures.filter((f: FixtureDto) => f.homeTeamId === uid || f.awayTeamId === uid);
    const userFixture = allFixtures.find((f: FixtureDto) =>
      (f.homeTeamId === uid || f.awayTeamId === uid) &&
      f.status !== 'Completed'
    );
    return userFixture || null;
  }, [leagueSetup?.fixtures, selectedTeam?.id]);

  const openLineup = async () => {
    const fixture = fixtureForUser();
    if (!fixture) return;

    // The manager picks the eleven first; the match is started from the lineup screen
    // with that choice, which the backend validates.
    navigate(`/match/lineup/${fixture.id}`);
  };

  const uf = fixtureForUser();
  const playDisabled = !uf;

  return (
    <div className="card league-screen">
      <div className="league-head">
        <div>
          <h2>Campeonato</h2>
          <div className="badge" style={{ display: 'inline-block', marginTop: '7px' }}>
            {finished ? 'CAMPEÃO DEFINIDO' : `Rodada ${currentRound + 1}/14`}
          </div>
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          <button className="ctrl">🔄 Nova temporada</button>
          <button className="primary" disabled={playDisabled} onClick={openLineup}>
            ⚽ Escalação e partida
          </button>
        </div>
      </div>

      <div className="league-grid">
        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className="league-panel">
            <h3>📊 Classificação</h3>
            <div id="standingsWrap">
              <StandingsTable standings={standings} userId={selectedTeam?.id} teams={leagueTeams} />
            </div>
          </div>

          <div className="league-panel">
            <h3>Jogos da rodada {currentRound + 1}</h3>
            <div id="roundFixtures">
              <FixtureList fixtures={fixtures} userId={selectedTeam?.id} />
            </div>
          </div>

          <div className="league-panel">
            <h3>📅 Calendário completo</h3>
            <div id="calendarList">
              <Calendar fixtures={leagueSetup?.fixtures || []} currentRound={currentRound} userId={selectedTeam?.id} />
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className="league-panel">
            <h3>⚽ Outros resultados</h3>
            <div id="otherResults">
              {completedOtherResults()}
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

function completedOtherResults() {
  return (
    <div className="league-note" style={{ margin: '0 0 6px' }}>Ainda não há resultados de outras partidas.</div>
  );
}

export default LeagueScreen;
