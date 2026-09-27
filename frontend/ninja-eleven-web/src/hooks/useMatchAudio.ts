import { useEffect, useRef, useState } from 'react';
import type { FeedEvent } from '@/state';
import { matchAudio } from '@/services/audioEngine';

interface UseMatchAudioArgs {
  /** The engine's ordered log, exactly as the match screen holds it. */
  feed: FeedEvent[];
  /** True while the match exists and has not finished. */
  live: boolean;
  /**
   * The match the log belongs to. Priming follows it: the feed lives in one store shared by
   * every screen, so a manager who goes from one match to another arrives on a log that
   * still holds the other one's events, and a hook that primed on "the first non-empty
   * feed" would sit there waiting for a sequence number that has nothing to do with this
   * match.
   */
  matchId: string;
}

/**
 * Wires the sound of a match to what the backend says happened.
 *
 * The engine's log is also how the feed was built, so a sound is played only for events
 * that arrive while the screen is open: the first batch is history — a manager who joins a
 * match at minute 60 should not hear the whistle of kick-off — and every later sequence is
 * new, and is played once even if it arrives by the hub and again by the recovery fetch.
 */
export const useMatchAudio = ({ feed, live, matchId }: UseMatchAudioArgs) => {
  const [muted, setMuted] = useState(matchAudio.isMuted());
  /** The last sequence already sounded, and whether the opening batch was seen yet. */
  const lastSequence = useRef(0);
  const primed = useRef(false);

  // The crowd belongs to the match, not to the screen: it starts with the whistle and stops
  // at the final one, or when the manager walks away from the tab.
  useEffect(() => {
    if (live) {
      matchAudio.startCrowd();
    } else {
      matchAudio.stopCrowd();
    }

    return () => matchAudio.stopCrowd();
  }, [live]);

  useEffect(() => {
    if (feed.length === 0) return;

    if (!primed.current) {
      // The log the screen opened with. Everything from here on is something that happened
      // while it was being watched.
      primed.current = true;
      lastSequence.current = feed[feed.length - 1]?.sequence ?? 0;
      return;
    }

    const fresh = feed.filter(event => event.sequence > lastSequence.current);
    if (fresh.length === 0) return;

    lastSequence.current = fresh[fresh.length - 1].sequence;

    // One sound at a time, and a broken sound does not take the rest of the batch with it:
    // a goal and a whistle in the same batch are two sounds, not one sound and an
    // exception.
    fresh.forEach(event => {
      try {
        matchAudio.reactToEvent(event);
      } catch (error) {
        console.error('Failed to play the match sound for', event.type, error);
      }
    });
  }, [feed]);

  // A new match starts from silence: the sequence of the previous one must not silence the
  // whistle of this one's kick-off, and this one's history must not be played as though it
  // had just happened.
  useEffect(() => {
    primed.current = false;
    lastSequence.current = 0;
  }, [matchId]);

  const toggleMuted = () => {
    const next = !matchAudio.isMuted();
    matchAudio.setMuted(next);
    setMuted(next);
  };

  return { muted, toggleMuted };
};
