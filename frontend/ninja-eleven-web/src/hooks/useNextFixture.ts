import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { CompetitionApi, FixtureApi, RoundApi, SeasonApi } from '@/api';
import type { CompetitionEditionDto, FixtureDto, RoundDto } from '@/types';

export interface NextFixture {
  fixture: FixtureDto;
  round: RoundDto;
  /** The matchday of the season the fixture is on, when the round belongs to one. */
  matchDayNumber: number | null;
  /** The edition the round is in: the competition, the division and the kind. */
  competition: CompetitionEditionDto | null;
}

/**
 * The fixture the manager is about to play: the earliest one of his club that has no match in
 * it yet, anywhere in the season.
 *
 * **The whole season, not the one competition on the filter.** A league season that has been
 * played to the end and a cup that is still running are both true, and a sidebar that reported
 * "nothing to play" while a cup tie was in four days would be wrong in the way that costs the
 * manager a match. So the walk is over every window of the calendar in the order they are
 * played — a championship window and a cup window on the same day are the same day, and which
 * of the two the club plays is the fixture's business, not the order of the competitions.
 *
 * It is asked of the backend rather than read out of a list already in the browser, because
 * "the next one" is a question about the whole calendar and not about a screen's copy of it.
 * The calendar arrives in one call, the editions in another, and the walk then stops at the
 * first fixture of the club that is still to be played — so a round already played costs one
 * request rather than one per round behind it.
 *
 * It re-asks whenever the route changes. A career is a sequence of matches, and the one the
 * sidebar offers has to be the one that is next after the match he just finished — a value
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
        const [calendar, editions] = await Promise.all([
          SeasonApi.getCalendar(seasonId, false),
          CompetitionApi.listEditionsBySeason(seasonId)
        ]);

        if (cancelled) {
          return;
        }

        const matchDays = new Map(calendar.matchDays.map(day => [day.id, day.number]));
        const competitions = new Map(editions.map(edition => [edition.id, edition]));

        // The order football is played in: the matchday first, and the window inside it, so a
        // cup tie on day 20 and a league game on day 19 are in the order they happen.
        const windows = [...calendar.windows].sort((left, right) => {
          const leftDay = left.matchDayId ? matchDays.get(left.matchDayId) ?? 0 : 0;
          const rightDay = right.matchDayId ? matchDays.get(right.matchDayId) ?? 0 : 0;
          return leftDay - rightDay || left.number - right.number;
        });

        for (const round of windows) {
          const fixtures = await FixtureApi.listByRound(round.id);
          const fixture = fixtures.find(
            item => (item.homeTeamId === teamId || item.awayTeamId === teamId) && !item.matchId
          );

          if (fixture) {
            if (!cancelled) {
              setNext({
                fixture,
                round,
                matchDayNumber: round.matchDayId ? matchDays.get(round.matchDayId) ?? null : null,
                competition: round.competitionSeasonId
                  ? competitions.get(round.competitionSeasonId) ?? null
                  : null
              });
            }
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
  }, [teamId, seasonId, location.pathname]);

  return { next, loading };
};
