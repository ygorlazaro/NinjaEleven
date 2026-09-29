import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { FixtureApi, SeasonApi } from '@/api';
import type { FixtureDto } from '@/types';

export interface NextFixture {
  fixture: FixtureDto;
  /** The matchday of the season the fixture is on, when the window belongs to one. */
  matchDayNumber: number | null;
  /** The edition the window belongs to: the competition and the division, as it is read. */
  competitionName: string;
  /** The kind of competition, which is what orders a matchday's windows. */
  competitionType: string;
  /**
   * Whether the fixture may be started right now.
   *
   * False is an answer and not a failure. A matchday is played in waves — Supercup,
   * championship, cup — and a cup leg on a day whose championship has not been played yet is
   * the club's next fixture while being unplayable. A box that offered it as kick-off time
   * sent a manager to a screen that could only refuse him, so the answer travels with the
   * fixture and the box says what he has to wait for.
   */
  playableNow: boolean;
  /** The wave the day is in while this fixture is not it, for the same reason. */
  waitingFor: string | null;
}

/**
 * The fixture the manager is about to play: the next one of his club in the order football
 * is actually played.
 *
 * **It is asked of the backend, and that is the whole point of it.** The order a season is
 * played in is a rule — a matchday is Supercup, then championship, then cup — while a window's
 * number is only an identifier: a championship window is numbered after its matchday and a
 * cup window by how many ties there have been, so the round of sixteen is window one of the
 * cup on a day whose championship is window five. A client that sorted a season's windows by
 * that number told a manager his next match was a cup leg, and the server then refused to
 * start it because the championship of the same day was still in front of it. The walk is a
 * rule, and a rule asked of the backend cannot be re-derived wrongly in a second place.
 *
 * **The whole season, not the one competition on a filter.** A league season played to the
 * end and a cup still running are both true, and a box that reported "nothing to play" while
 * a cup tie was in four days would be wrong in the way that costs a manager a match.
 *
 * It re-asks whenever the route changes. A career is a sequence of matches, and the one the
 * column offers has to be the one that is next after the match he just finished — a value
 * fetched once and kept would keep offering a game that has already been played.
 */
export const useNextFixture = (
  teamId: string | undefined,
  seasonId: string | undefined
): { next: NextFixture | null; loading: boolean } => {
  const [next, setNext] = useState<NextFixture | null>(null);
  const [loading, setLoading] = useState(false);
  const location = useLocation();

  useEffect(() => {
    if (!teamId || !seasonId) {
      setNext(null);
      return undefined;
    }

    let cancelled = false;

    const load = async () => {
      setLoading(true);

      try {
        const found = await FixtureApi.getNext(teamId, seasonId);

        if (cancelled) {
          return;
        }

        setNext(
          found
            ? {
                fixture: found.fixture,
                matchDayNumber: found.matchDayNumber ?? null,
                competitionName: found.competitionName,
                competitionType: found.competitionType,
                playableNow: found.waveOpen,
                waitingFor: found.waitingFor || null,
              }
            : null
        );
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
  }, [teamId, seasonId, location.pathname]);

  return { next, loading };
};
