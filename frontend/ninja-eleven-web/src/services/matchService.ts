import { MatchApi } from '@/api';
import { useGameState } from '@/state';
import type { FeedEvent } from '@/state';
import type { MatchEngineEventDto } from '@/types';

export function useMatchEngine() {
  const currentMatch = useGameState((s) => s.currentMatch);
  const setCurrentMatch = useGameState((s) => s.setCurrentMatch);
  const addFeedEvent = useGameState((s) => s.addFeedEvent);
  const setFeed = useGameState((s) => s.setFeed);
  const setLastEvents = useGameState((s) => s.setLastEvents);

  const convertToFeedEvent = (event: MatchEngineEventDto): FeedEvent => ({
    sequence: event.sequence,
    minute: event.minute,
    description: event.description,
    icon: event.icon,
    type: event.type,
    teamId: event.teamId,
    playerId: event.playerId,
    homeScore: event.homeScore,
    awayScore: event.awayScore,
    onTarget: event.type === 'Shot' || event.type === 'Save',
    isGoal: event.type === 'GoalScored' || event.type === 'OwnGoalScored',
  });

  const startMatch = async (fixtureId: string) => {
    const result = await MatchApi.start(fixtureId);
    if (result.events) {
      const feedEvents = result.events.map(convertToFeedEvent);
      setFeed(feedEvents);
      setLastEvents(result.events);
    }
    return result;
  };

  const tick = async (matchId: string) => {
    const events = await MatchApi.tick(matchId);
    if (events && events.length > 0) {
      const feedEvents = events.map(convertToFeedEvent);
      const currentFeed = useGameState.getState().feed;
      setFeed([...currentFeed, ...feedEvents]);
      setLastEvents(events);
    }
    return events;
  };

  const makeSubstitution = async (matchId: string, teamId: string, playerOut: string, playerIn: string) => {
    return await MatchApi.substitute(matchId, teamId, playerOut, playerIn);
  };

  const selectPenaltyTaker = async (matchId: string, teamId: string, playerId: string) => {
    return await MatchApi.selectPenaltyTaker(matchId, teamId, playerId);
  };

  const changeSpeed = async (matchId: string, speed: number) => {
    return await MatchApi.changeSpeed(matchId, speed);
  };

  const pauseMatch = async (matchId: string) => {
    const result = await MatchApi.pause(matchId);
    useGameState.getState().setIsPaused(true);
    return result;
  };

  const resumeMatch = async (matchId: string) => {
    const result = await MatchApi.resume(matchId);
    useGameState.getState().setIsPaused(false);
    return result;
  };

  const continueSecondHalf = async (matchId: string) => {
    return await MatchApi.continueSecondHalf(matchId);
  };

  const getMatchResult = async (matchId: string) => {
    return await MatchApi.getResult(matchId);
  };

  const getLineup = async (matchId: string, userTeamId?: string) => {
    return await MatchApi.getLineup(matchId, userTeamId);
  };

  return {
    startMatch,
    tick,
    makeSubstitution,
    selectPenaltyTaker,
    changeSpeed,
    pauseMatch,
    resumeMatch,
    continueSecondHalf,
    getMatchResult,
    getLineup,
    currentMatch,
  };
}
