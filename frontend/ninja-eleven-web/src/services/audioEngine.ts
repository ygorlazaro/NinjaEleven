/**
 * The sound of a match.
 *
 * One engine owns the audio elements so a whistle is never started twice and the crowd is
 * never layered on itself. It knows three things: the whistle, a goal out of a small pool,
 * and the crowd that runs under the whole match.
 *
 * Browsers refuse to play audio until the page has been interacted with, and there is no
 * way to ask whether that has happened — so the engine plays, and only waits if the play
 * was refused. A match screen is always reached by a click, which means the attempt
 * normally succeeds straight away, and a refusal means the very next click is what unlocks
 * the stadium.
 */

/** Kept quiet on purpose: it is a stadium behind the match, not the match itself. */
const CROWD_VOLUME = 0.25;
const WHISTLE_VOLUME = 0.7;
const GOAL_VOLUME = 0.85;

/** A goal is never the same twice: the pool is drawn at random every time. */
const GOAL_SOUNDS = [
  '/audio/goal.mp3',
  '/audio/goal1.mp3',
  '/audio/goal2.mp3',
  '/audio/goal3.mp3',
  '/audio/goal4.mp3',
];

/** The whistle answers the moments football is played around: the whistle itself. */
const WHISTLE_EVENTS = new Set([
  'KickOff',           // the first whistle of the match
  'HalfTimeReached',   // the end of the first half
  'SecondHalfStarted', // the whistle that starts the second half
  'MatchFinished',     // the end of the match
  'Foul',              // the referee stops the game
]);

const GOAL_EVENTS = new Set(['GoalScored', 'OwnGoalScored']);

/**
 * How many refused sounds are remembered. Short on purpose: the sounds held back are the
 * most recent ones, because a goal the manager missed is still worth hearing and a goal
 * from the first half is not.
 */
const MAX_DEFERRED_SOUNDS = 3;

class MatchAudioEngine {
  private readonly whistle: HTMLAudioElement;
  private readonly crowd: HTMLAudioElement;
  private readonly goals: HTMLAudioElement[];

  private muted = false;
  /**
   * Whether this page may play audio. `null` means nobody has tried yet, which is why the
   * first sound is always attempted instead of being waited on.
   */
  private unlocked: boolean | null = null;
  private crowdWanted = false;
  private unlockBound = false;
  private goalsWarmed = false;
  /** The sounds refused because the page had not been interacted with yet. */
  private pending: (() => Promise<unknown>)[] = [];

  constructor() {
    this.whistle = this.create('/audio/whistle.mp3', WHISTLE_VOLUME);
    this.crowd = this.create('/audio/crowd.mp3', CROWD_VOLUME);
    this.crowd.loop = true;
    this.goals = GOAL_SOUNDS.map(src => this.create(src, GOAL_VOLUME));
  }

  private create(src: string, volume: number): HTMLAudioElement {
    const audio = new Audio(src);
    audio.preload = 'auto';
    audio.volume = volume;
    return audio;
  }

  /**
   * Asks an element to play, and always hands back a promise.
   *
   * The lib this project compiles against types `play()` as `void`, so the rejection a
   * browser reports for a refused sound is invisible unless it is normalised here. That
   * rejection is the only way this engine learns whether the page may play, which is why
   * the helper exists rather than a `.catch` at each call site.
   */
  private static safePlay(audio: HTMLAudioElement): Promise<void> {
    try {
      return Promise.resolve(audio.play());
    } catch (error) {
      // An element that throws instead of rejecting is still a refusal, not a crash.
      return Promise.reject(error instanceof Error ? error : new Error(String(error)));
    }
  }

  /**
   * Plays a sound now. If the browser refuses because nobody has interacted with the page
   * yet, the sound waits for the first click instead of being lost: a whistle that only
   * sounds for a manager who happens to click at the right moment is not a whistle.
   */
  private playNow(audio: HTMLAudioElement, volume: number, restart: boolean): void {
    if (this.muted) return;

    if (restart) {
      // Rewinding an element whose data has not arrived is the one operation here that a
      // browser may refuse, and a refusal must not cost the sound: the manager never learns
      // whether the goal was heard or whether the page threw.
      try {
        audio.currentTime = 0;
      } catch {
        // Nothing to rewind yet — the file is still on its way in.
      }
    }

    audio.volume = volume;

    // The play is always attempted, even when the engine already believes the page is
    // locked. Believing that was the bug: a match screen reached by a refresh has no user
    // gesture behind it, the first sound is refused, and from then on every goal was only
    // ever queued — so a goal happened on screen and made no sound at all, and it stayed
    // that way for as long as the manager did not click something. Asking costs nothing
    // when the answer is still no, and it is the only way the engine ever notices that the
    // page has become allowed to play.
    void MatchAudioEngine.safePlay(audio).then(
      () => {
        this.unlocked = true;
      },
      () => {
        this.unlocked = false;
        this.defer(() => MatchAudioEngine.safePlay(audio));
        this.bindUnlock();
      }
    );
  }

  /**
   * Holds a sound until the page is allowed to play. The backlog is deliberately short: a
   * goal from two minutes ago is not something anybody wants to hear on the next click, and
   * a queue that grew without limit turned one unlock into a burst of every sound the
   * match had missed.
   */
  private defer(play: () => Promise<unknown>): void {
    this.pending.push(play);

    while (this.pending.length > MAX_DEFERRED_SOUNDS) {
      this.pending.shift();
    }
  }

  /** Waits for the first click or keypress, then replays whatever the refusal held back. */
  private bindUnlock(): void {
    if (this.unlockBound || typeof window === 'undefined') return;
    this.unlockBound = true;

    const unlock = () => {
      this.unlocked = true;

      const queued = this.pending;
      this.pending = [];
      queued.forEach(play => {
        // A deferred sound can still be refused — a click is not always enough, and a sound
        // that is refused again is dropped rather than kept, or it would queue forever.
        void play().catch(() => undefined);
      });

      // The crowd is not a one-shot, so it is asked for again rather than replayed.
      if (this.crowdWanted) this.startCrowd();

      window.removeEventListener('pointerdown', unlock);
      window.removeEventListener('keydown', unlock);
      this.unlockBound = false;
    };

    window.addEventListener('pointerdown', unlock);
    window.addEventListener('keydown', unlock);
  }

  /**
   * The whistle, for the start and the end of each half and for a foul. A restart is what
   * a referee does: the sound is cut and blown again, never layered on itself.
   */
  whistleBlow(): void {
    this.playNow(this.whistle, WHISTLE_VOLUME, true);
  }

  /** One goal, out of the pool, at random. */
  goal(): void {
    const chosen = this.goals[Math.floor(Math.random() * this.goals.length)];

    if (chosen) {
      // The pool is pulled in as soon as the page may play at all, so the first goal of a
      // match is a sound rather than a download. A goal is the one moment of a match that
      // cannot be late.
      this.warmGoals();
      this.playNow(chosen, GOAL_VOLUME, true);
    }
  }

  /** Fetches the goal sounds ahead of the first goal, at most once. */
  private warmGoals(): void {
    if (this.goalsWarmed) return;
    this.goalsWarmed = true;
    this.goals.forEach(goal => { void Promise.resolve(goal.load()).catch(() => undefined); });
  }

  /**
   * Reacts to one engine event. The mapping is the whole rulebook of the sound: a whistle
   * for the moments the referee speaks and a goal for a goal, and silence for everything
   * else, because a corner is not worth a sound.
   */
  reactToEvent(type: string): void {
    if (WHISTLE_EVENTS.has(type)) {
      this.whistleBlow();
      return;
    }

    if (GOAL_EVENTS.has(type)) {
      this.goal();
    }
  }

  /**
   * The crowd runs for as long as the match is being watched, from the first whistle to the
   * last. Asking for it twice is harmless: the same element is reused.
   */
  startCrowd(): void {
    this.crowdWanted = true;
    if (this.muted) return;
    if (this.unlocked === false) {
      this.bindUnlock();
      return;
    }

    const start = () => MatchAudioEngine.safePlay(this.crowd).then(
      () => {
        this.unlocked = true;
      },
      () => {
        this.unlocked = false;
        this.bindUnlock();
      }
    );

    // An already running crowd is left alone, so a re-render cannot restart the stadium.
    if (this.crowd.paused) start();
  }

  /** The match is over, or the manager left the screen: the stadium goes quiet. */
  stopCrowd(): void {
    this.crowdWanted = false;
    this.crowd.pause();
    this.crowd.currentTime = 0;
  }

  setMuted(muted: boolean): void {
    this.muted = muted;

    if (muted) {
      this.stopCrowd();
      this.pending = [];
      return;
    }

    if (this.crowdWanted) this.startCrowd();
  }

  isMuted(): boolean {
    return this.muted;
  }
}

/**
 * One engine for the whole app. Two would mean two crowds and two whistles, and React mounts
 * effects twice in development.
 */
export const matchAudio = new MatchAudioEngine();
