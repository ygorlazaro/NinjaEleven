import { useEffect, useState } from 'react';
import { TeamApi } from '@/api';
import { useGameState } from '@/state';

export type CareerCheck = 'checking' | 'fresh' | 'resumed';

/**
 * Whether the club remembered in this browser is still a club.
 *
 * A career is persisted so a reload does not send the manager back to the club list in the
 * middle of a season. But a memory of a club is not a club: the world can be reseeded, a club
 * can be deleted, and a division can be reorganised between one visit and the next. Landing
 * on `/team/{id}` for a club the backend has never heard of is a screen that cannot say
 * anything — no squad, no fixtures, no table — and the only way out of it is to guess at the
 * URL bar.
 *
 * So the memory is checked against the backend before it is acted on, and a club that is not
 * there is forgotten rather than shown. `forgetClub` is what clears it, and the manager lands
 * on the club list, which is the screen whose job is to offer a club.
 *
 * The check is a single list of clubs for the whole pyramid, so it costs one request and it
 * also tells the club picker what it is picking from.
 */
export function useCareerCheck(): CareerCheck {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const forgetClub = useGameState((s) => s.forgetClub);

  const [status, setStatus] = useState<CareerCheck>(selectedTeam ? 'checking' : 'fresh');

  useEffect(() => {
    let cancelled = false;

    // No remembered club: nothing to check, and nothing to be wrong about.
    if (!selectedTeam) {
      setStatus('fresh');
      return () => { cancelled = true; };
    }

    const verify = async () => {
      try {
        const clubs = await TeamApi.list();
        if (cancelled) return;

        const stillExists = clubs.some(club => club.id === selectedTeam.id);

        if (stillExists) {
          setLeagueTeams(clubs);
          setStatus('resumed');
        } else {
          // The club the browser remembers is not in the world any more. The stored squad
          // list goes with it: it was fetched for a world that no longer exists.
          forgetClub();
          setStatus('fresh');
        }
      } catch {
        // The backend could not be asked. That is not the same as the club being gone, and
        // forgetting a manager's career because a request timed out would be a punishment
        // for a network. The screen says the server is unreachable instead.
        if (!cancelled) setStatus('resumed');
      }
    };

    verify();

    return () => { cancelled = true; };
    // `selectedTeam` is watched so that a club chosen after the first check is verified too.
  }, [selectedTeam?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  return status;
}
