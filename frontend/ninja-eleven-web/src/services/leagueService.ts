import { LeagueApi } from '@/api';
import { useGameState } from '@/state';
import type { FixtureDto, LeagueSetupResult, TeamDto } from '@/types';

export function useLeagueSetup() {
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const setStandings = useGameState((s) => s.setStandings);
  const setLeagueSetup = useGameState((s) => s.setLeagueSetup);
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);

  const initializeLeague = async (competitionId: string, seasonId: string, teamIds: string[]) => {
    const setup: LeagueSetupResult = await LeagueApi.setup(competitionId, seasonId, teamIds);
    setLeagueSetup(setup);
    return setup;
  };

  // Both tables travel together, so the store is handed both. A store that kept only the
  // official one is a store that has thrown away the live half, and the screen that wanted
  // it would be back to working it out for itself.
  const refreshStandings = async (compSeasonId: string) => {
    const standings = await LeagueApi.getStandings(compSeasonId);
    setStandings(standings);
  };

  // There is deliberately no "load the season's scorers" here. A season is four divisions
  // and a cup, so a chart asked of a season alone is a chart of the whole country, and the
  // store field it would fill is the one the league screen renders — a helper that wrote it
  // would replace the manager's own division's artilharia with a table mixing all ninety-six
  // clubs, and the manager would be reading the wrong chart without anything looking broken.
  // The league screen asks for its division; the cup screen asks for the cup.

  return { initializeLeague, refreshStandings };
}
