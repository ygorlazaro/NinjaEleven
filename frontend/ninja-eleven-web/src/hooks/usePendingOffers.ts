import { useEffect, useState } from 'react';
import { SeasonApi, TransferApi } from '@/api';

/**
 * How many offers are waiting for the manager to answer.
 *
 * An offer the club never answers dies at the end of the season, and a player nobody answered
 * for stays "spoken for" in the market for all of it — so an unread inbox is not a pile of
 * paperwork, it is a set of decisions that are being made by default. The count is on the
 * sidebar for that reason: the manager finds out that there is something to decide from every
 * screen, and not only from the one he remembered to open.
 *
 * It is a count and not the list, because the list is the market screen's job and a count fits
 * in a column. When there is no club, there is nothing waiting: the count is zero rather than
 * unknown, and the question is not asked at all.
 */
export function usePendingOfferCount(
  clubId: string | undefined,
  seasonId: string | undefined
): number {
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!clubId || !seasonId) {
      setCount(0);
      return undefined;
    }

    let alive = true;

    const load = () => {
      TransferApi.getInbox(clubId, seasonId)
        .then(inbox => {
          if (alive) setCount(inbox.incoming.filter(proposal => proposal.status === 'Pending').length);
        })
        // A failure here is a sidebar badge that is late, not a screen that is broken. The
        // market itself still loads and still answers.
        .catch(() => { if (alive) setCount(0); });
    };

    load();

    // The count is polled rather than pushed: offers are made by the matchday sweep and by the
    // other clubs' turns, and a hub that carried the market would be a second live channel to
    // keep alive for a number. Sixty seconds is often enough to be useful and slow enough to
    // cost nothing, and the count refreshes itself the moment the market screen is opened.
    const timer = window.setInterval(load, 60_000);

    return () => {
      alive = false;
      window.clearInterval(timer);
    };
  }, [clubId, seasonId]);

  return count;
}

/**
 * The season the offer count is read against. The sidebar has to know which season the world
 * is in for the same reason the market does: an offer belongs to the season it was made in,
 * and the current season is the only one whose inbox is the manager's business.
 */
export function useCurrentSeasonId(): string | undefined {
  const [seasonId, setSeasonId] = useState<string | undefined>(undefined);

  useEffect(() => {
    let alive = true;

    SeasonApi.current()
      .then(season => { if (alive) setSeasonId(season.id); })
      .catch(() => { if (alive) setSeasonId(undefined); });

    return () => { alive = false; };
  }, []);

  return seasonId;
}
