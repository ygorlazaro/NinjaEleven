import { useEffect, useState } from 'react';
import { MatchApi } from '@/api';
import type { LiveMatchDto } from '@/types';

/** How often the badge is refreshed, in milliseconds. */
const POLL_MS = 5_000;

/**
 * The match the manager's club is playing right now, or null when it is not playing one.
 *
 * A manager managing a club is somewhere else for most of a matchday — the market, the
 * calendar, a profile — and a club that is mid-game has to be one click away rather than
 * something he goes and looks for. The badge is the answer: it says the club is playing, who
 * it is against, at what score and in what minute, so a manager glancing at the column learns
 * all of it without opening anything.
 *
 * **It is polled, and it is polled only while there is something to poll for.** Once the
 * answer is a live match, the badge is the only thing standing between the manager and a
 * match he is not watching, so it is refreshed every few seconds; before that — which is most
 * of a season's hours — the question is asked twice and then left alone, and it is asked again
 * the moment the route changes, because a match is started from the lineup screen and a badge
 * that waited out its interval would show the match twenty seconds late.
 *
 * The score, the minute and the half-time flag are the backend's, read off the same live state
 * the match screen follows. The badge adds the shield and the word "ao vivo"; it does not work
 * out whether a game is running.
 */
export function useLiveMatch(clubId: string | undefined, routeKey: string): LiveMatchDto | null {
  const [live, setLive] = useState<LiveMatchDto | null>(null);

  useEffect(() => {
    if (!clubId) {
      setLive(null);
      return undefined;
    }

    let alive = true;
    let timer: number | undefined;

    const load = () => {
      MatchApi.getLiveForTeam(clubId)
        .then(match => {
          if (!alive) return;
          setLive(match ?? null);

          // A badge for a finished match is a lie with a countdown on it, so the moment the
          // answer stops being a live match the polling stops too — and the badge goes with
          // it, rather than sitting there claiming a club is mid-game until the next tick.
          if (match && timer === undefined) {
            timer = window.setInterval(load, POLL_MS);
          } else if (!match && timer !== undefined) {
            window.clearInterval(timer);
            timer = undefined;
          }
        })
        // A failure here is a badge that is late, not a screen that is broken: the match
        // itself is on the hub and the manager reaches it by name.
        .catch(() => { if (alive) setLive(null); });
    };

    load();

    return () => {
      alive = false;
      if (timer !== undefined) window.clearInterval(timer);
    };
  }, [clubId, routeKey]);

  return live;
}
