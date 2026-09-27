import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { FixtureApi, RoundApi } from '@/api';
import type { FixtureDto, RoundDto } from '@/types';

export interface NextFixture {
  fixture: FixtureDto;
  round: RoundDto;
}

/**
 * The fixture the manager is about to play: the earliest one of his club that has no match
 * in it yet.
 *
 * It is asked of the backend round by round rather than read out of a list already in the
 * browser, because "the next one" is a question about the whole calendar and not about a
 * screen's copy of it. The rounds come back in order and the walk stops at the first fixture
 * of the club that is still to be played, so a round already played costs one request rather
 * than one per round behind it.
 *
 * It re-asks whenever the route changes. A career is a sequence of matches, and the one the
 * sidebar offers has to be the one that is next after the match he just finished — a value
 * fetched once and kept would keep offering a game that has already been played.
 */
export const useNextFixture = (
  teamId: string | undefined,
  competitionSeasonId: string | undefined
): { next: NextFixture | null; loading: boolean } => {
  const [next, setNext] = useState<NextFixture | null>(null);
  const [loading, setLoading] = useState(false);
  const location = useLocation();

  useEffect(() => {
    if (!teamId || !competitionSeasonId) {
      setNext(null);
      return undefined;
    }

    let cancelled = false;

    const load = async () => {
      setLoading(true);

      try {
        const rounds = (await RoundApi.listByCompetitionSeason(competitionSeasonId))
          .sort((a, b) => a.number - b.number);

        for (const round of rounds) {
          const fixtures = await FixtureApi.listByRound(round.id);

          const fixture = fixtures.find(
            item =>
              (item.homeTeamId === teamId || item.awayTeamId === teamId) && !item.matchId
          );

          if (fixture) {
            if (!cancelled) setNext({ fixture, round });
            return;
          }
        }

        if (!cancelled) setNext(null);
      } catch (err) {
        console.error('Failed to find the next fixture:', err);
        if (!cancelled) setNext(null);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [teamId, competitionSeasonId, location.pathname]);

  return { next, loading };
};
