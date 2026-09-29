import { useEffect, useState } from 'react';
import { InboxApi } from '@/api';

/**
 * How many messages the manager has not opened. The number on the column.
 *
 * It is a count and not the list, because the list is the box screen's job and a count fits in
 * a column. The engine writes a message the moment it decides something has happened — the
 * whistle of a finished match, a line of the ledger, a bid on one of the club's men — and a
 * manager who only found out by opening the box would be finding out about a defeat twenty
 * matches later.
 *
 * It is polled rather than pushed for the same reason the pending-offer badge is: the box is
 * written from the match loop, the season close and the market, and a hub that carried it
 * would be a second live channel to keep alive for one number. Thirty seconds is often enough
 * to be useful and slow enough to cost nothing.
 *
 * With no club there is nothing waiting, so the count is zero rather than unknown and the
 * question is not asked at all.
 */
export function useUnreadMessageCount(clubId: string | undefined, refreshKey?: string): number {
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!clubId) {
      setCount(0);
      return undefined;
    }

    let alive = true;

    const load = () => {
      InboxApi.getUnreadCount(clubId)
        .then(unread => { if (alive) setCount(unread); })
        // A failure here is a badge that is late, not a screen that is broken: the box itself
        // still loads and still answers.
        .catch(() => { if (alive) setCount(0); });
    };

    load();

    const timer = window.setInterval(load, 30_000);

    return () => {
      alive = false;
      window.clearInterval(timer);
    };
    // The path is in the dependency list so that reading the box itself refreshes the badge:
    // a manager who has just marked everything read comes back to a column that says zero,
    // not to a column still counting what he has already read.
  }, [clubId, refreshKey]);

  return count;
}
