import { LeagueApi } from '@/api';
import { useGameState } from '@/state';
import type { FixtureDto, LeagueSetupResult, TeamDto } from '@/types';

export function useLeagueSetup() {
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const setStandings = useGameState((s) => s.setStandings);
  const setFixtures = useGameState((s) => s.setFixtures);
  const setScorers = useGameState((s) => s.setScorers);
  const setLeagueSetup = useGameState((s) => s.setLeagueSetup);
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);

  const initializeLeague = async (competitionId: string, seasonId: string, teamIds: string[]) => {
    const setup: LeagueSetupResult = await LeagueApi.setup(competitionId, seasonId, teamIds);
    setLeagueSetup(setup);
    return setup;
  };

  const loadSeasonData = async (seasonId: string) => {
    const [scorers] = await Promise.all([
      LeagueApi.getScorers(seasonId),
    ]);
    setScorers(scorers);
  };

  const refreshStandings = async (compSeasonId: string) => {
    const standings = await LeagueApi.getStandings(compSeasonId);
    setStandings(standings);
  };

  return { initializeLeague, loadSeasonData, refreshStandings };
}
