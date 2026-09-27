import * as signalR from '@microsoft/signalr';
import { HUB_BASE_URL, MATCH_HUB_PATH } from '@/config/env';
import type {
  MatchCommandResult,
  MatchEngineEventDto,
  MatchdayEventDto,
  MatchResult,
  MatchStateDto
} from '../types';
import type { MatchScore } from '@/components/Match/MatchdayScoreboard';

type Listener<T> = (payload: T) => void;

/**
 * Thin client over MatchHub. The frontend never decides anything about the game: it
 * subscribes to a match, receives the snapshot plus the ordered event stream, and sends
 * commands back. The server owns the clock.
 *
 * Connecting and disconnecting are serialized through one promise chain. React mounts
 * an effect twice in development, so a connect and a disconnect can otherwise overlap:
 * the disconnect would stop a negotiation that had not finished yet, and the second
 * mount would find a connection that is being torn down.
 */
class MatchHubClient {
  private connection: signalR.HubConnection | null = null;
  private currentMatchId: string | null = null;
  private lifecycle: Promise<unknown> = Promise.resolve();

  /**
   * The round whose scores the client follows, remembered so a reconnect can ask again.
   *
   * After a reconnect the server holds no group for us: the membership went with the
   * connection. A client that only restored its match subscription came back to a match
   * it could watch and a matchday scoreboard that stayed empty for the rest of the game.
   */
  private currentRoundId: string | null = null;

  /**
   * Every connect hands a lease to its caller and keeps the newest one. A disconnect
   * only closes the connection when it presents the current lease, so a cleanup that
   * arrives late cannot close the connection the next mount just opened.
   */
  private leaseCounter = 0;
  private activeLease = 0;

  private stateListeners = new Set<Listener<MatchStateDto>>();
  private eventListeners = new Set<Listener<MatchEngineEventDto[]>>();
  private resultListeners = new Set<Listener<MatchResult>>();
  private scoreListeners = new Set<Listener<MatchScore>>();
  private matchdayEventListeners = new Set<Listener<MatchdayEventDto>>();

  /**
   * Subscribes to a match and returns the lease that releases it. React mounts an
   * effect twice in development, so a connect and a disconnect can otherwise overlap:
   * the disconnect would stop a negotiation that had not finished yet, and the second
   * mount would find a connection that is being torn down.
   */
  connect(matchId: string): number {
    const lease = ++this.leaseCounter;
    this.activeLease = lease;
    this.currentMatchId = matchId;

    this.serialize(() => this.connectCore(matchId, lease)).catch(error => {
      if (this.activeLease === lease) {
        console.error('Failed to connect to the match hub:', error);
      }
    });

    return lease;
  }

  private async connectCore(matchId: string, lease: number): Promise<void> {
    if (!this.connection) {
      // withCredentials: false keeps the negotiation a simple request. The app has no
      // cookie session, so credentials are never needed, and not asking for them keeps
      // the CORS preflight of POST /matchHub/negotiate free of any extra requirement.
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${HUB_BASE_URL}${MATCH_HUB_PATH}`, { withCredentials: false })
        .withAutomaticReconnect()
        .build();

      this.connection.on('MatchEvent', (events: MatchEngineEventDto[]) => {
        this.eventListeners.forEach(listener => listener(events));
      });

      this.connection.on('MatchState', (state: MatchStateDto) => {
        this.stateListeners.forEach(listener => listener(state));
      });

      this.connection.on('MatchFinished', (result: MatchResult) => {
        this.resultListeners.forEach(listener => listener(result));
      });

      // The score of every match of the round, including the ones the client is not
      // watching. It is what the scoreboard of the matchday is built from.
      this.connection.on('MatchScore', (score: MatchScore) => {
        this.scoreListeners.forEach(listener => listener(score));
      });

      // The beats of the matches of the round the client is not watching in full. A
      // scoreboard that can only say 1 x 0 does not say who scored, and a round the
      // manager is not playing in is still a round he is watching.
      this.connection.on('MatchdayEvent', (payload: MatchdayEventDto) => {
        this.matchdayEventListeners.forEach(listener => listener(payload));
      });

      // After a reconnect the server has no group for us any more, so we subscribe
      // again — to the match and to the round, since the matchday scoreboard is fed by a
      // group that went away with the connection too.
      this.connection.onreconnected(async () => {
        if (this.currentMatchId) {
          await this.subscribe(this.currentMatchId);
        }
        if (this.currentRoundId) {
          // Use subscribeMatchday logic to ensure connection is ready
          await this.subscribeMatchday(this.currentRoundId);
        }
      });
    }

    if (this.activeLease !== lease) {
      // A newer mount already took over while this one was queued.
      return;
    }

    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      await this.connection.start();
    }

    await this.subscribe(matchId);
  }

  private async subscribe(matchId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    const state = await this.connection.invoke<MatchStateDto>('SubscribeMatch', matchId);
    this.stateListeners.forEach(listener => listener(state));
  }

  /**
   * Runs the connect and the disconnect one at a time, in the order they were asked
   * for. A failing step does not block the next one.
   */
  private serialize(work: () => Promise<void>): Promise<void> {
    const next = this.lifecycle.then(work, work);
    this.lifecycle = next.catch(() => undefined);
    return next;
  }

  /** Returns the function that removes the listener again. */
  onState(listener: Listener<MatchStateDto>): () => void {
    this.stateListeners.add(listener);
    return () => this.stateListeners.delete(listener);
  }

  /** Returns the function that removes the listener again. */
  onEvent(listener: Listener<MatchEngineEventDto[]>): () => void {
    this.eventListeners.add(listener);
    return () => this.eventListeners.delete(listener);
  }

  /** Returns the function that removes the listener again. */
  onResult(listener: Listener<MatchResult>): () => void {
    this.resultListeners.add(listener);
    return () => this.resultListeners.delete(listener);
  }

  /** Returns the function that removes the listener again. */
  onScore(listener: Listener<MatchScore>): () => void {
    this.scoreListeners.add(listener);
    return () => this.scoreListeners.delete(listener);
  }

  /** Returns the function that removes the listener again. */
  onMatchdayEvent(listener: Listener<MatchdayEventDto>): () => void {
    this.matchdayEventListeners.add(listener);
    return () => this.matchdayEventListeners.delete(listener);
  }

  async pause(matchId: string) {
    return this.invoke('PauseMatch', { matchId });
  }

  async resume(matchId: string) {
    return this.invoke('ResumeMatch', { matchId });
  }

  async changeSpeed(matchId: string, speed: number) {
    return this.invoke('ChangeMatchSpeed', { matchId, speed });
  }

  /**
   * Joins the score stream of a round. The same connection that follows the match in
   * full also reports the other matches of the matchday.
   *
   * The round is not known when the screen mounts — it comes from the fixture list, which
   * is another request — so this call can easily arrive before the connection is up. A
   * plain invoke in that moment is dropped, and the subscription is never made for the
   * rest of the match: the manager watches his own game in full and the matchday
   * scoreboard stays empty, with nothing in the logs to explain it. So it waits for the
   * connection instead of losing the race.
   */
  subscribeMatchday(roundId: string): Promise<void> {
    if (!roundId) {
      return Promise.resolve();
    }

    this.currentRoundId = roundId;

    return this.serialize(async () => {
      if (!this.connection) {
        return;
      }

      // Wait for connection to be ready, then join the round
      if (this.connection.state === signalR.HubConnectionState.Disconnected) {
        await this.connection.start();
      }

      // If still connecting, wait for it
      if (this.connection.state === signalR.HubConnectionState.Connecting) {
        await new Promise<void>((resolve, reject) => {
          const timeout = setTimeout(() => reject(new Error('Connection timeout')), 10000);
          this.connection!.onreconnected(() => {
            clearTimeout(timeout);
            resolve();
          });
          // Also resolve if already connected
          if (this.connection!.state === signalR.HubConnectionState.Connected) {
            clearTimeout(timeout);
            resolve();
          }
        }).catch(() => {
          // Ignore timeout, will try onreconnected
        });
      }

      if (this.connection.state === signalR.HubConnectionState.Connected) {
        await this.joinRound(roundId);
      }
    }).catch(error => {
      console.error('Failed to subscribe to the round scores:', error);
    });
  }

  private async joinRound(roundId: string): Promise<unknown> {
    if (!this.connection) {
      return;
    }

    // Wait for connection to be ready
    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      await this.connection.start();
    }

    if (this.connection.state === signalR.HubConnectionState.Connecting) {
      // `onreconnected` is a setter and not a subscription: it holds one callback and a
      // second call replaces the first, so there is nothing to take off again afterwards.
      // The wait is therefore settled once and only once, by a flag the two paths share —
      // a reconnect arriving on its own and the connection being up already both settle it,
      // and neither can settle it twice.
      await new Promise<void>(resolve => {
        let settled = false;
        const settle = () => {
          if (settled) return;
          settled = true;
          clearTimeout(timeout);
          resolve();
        };
        const timeout = setTimeout(settle, 10000);
        this.connection!.onreconnected(settle);
        // Also resolve if already connected
        if (this.connection!.state === signalR.HubConnectionState.Connected) {
          settle();
        }
      });
    }

    if (this.connection.state === signalR.HubConnectionState.Connected) {
      // The round id goes as the argument itself, not wrapped in an object. SignalR binds
      // arguments by position: a hub method declared `SubscribeMatchday(Guid roundId)` is
      // sent `arguments: ["<the guid>"]`, and handing it `{ roundId }` instead is not a
      // shape it can be lenient about — the binder tries to read a Guid and finds an
      // object, and the join is refused with "Parameters to hub method are incorrect".
      //
      // That refusal is silent from the manager's side: the scoreboard simply never moves,
      // while the backend is publishing every other match of the matchday to a group nobody
      // is in. The methods that take a DTO really are called with an object — that is their
      // one argument — which is why the rest of the screen works and this does not.
      return this.connection.invoke('SubscribeMatchday', roundId);
    }
  }

  async continueSecondHalf(matchId: string) {
    return this.invoke('ContinueSecondHalf', { matchId });
  }

  /**
   * Explicitly leave the round group (call when truly navigating away).
   *
   * It goes through the same queue as {@link subscribeMatchday}, and that is the whole
   * point: a leave issued while a join is still waiting for the connection would otherwise
   * go straight out and overtake it, and the screen would end up having left a group it had
   * only just joined. Ordered, the last word on membership is the last thing asked for.
   */
  async leaveRound(roundId: string): Promise<void> {
    if (!roundId) {
      return;
    }

    await this.serialize(async () => {
      // A leave for a round is not a leave for whichever round is current now: a manager who
      // has already moved on to the next matchday keeps following that one.
      if (this.currentRoundId !== roundId) {
        return;
      }

      if (this.connection?.state === signalR.HubConnectionState.Connected) {
        try {
          await this.connection.invoke('LeaveMatchday', roundId);
        } catch {
          // The connection may already be gone; nothing left to leave.
        }
      }

      this.currentRoundId = null;
    }).catch(() => undefined);
  }

  async makeSubstitution(matchId: string, teamId: string, playerOutId: string, playerInId: string) {
    return this.invoke('MakeSubstitution', { matchId, teamId, playerOutId, playerInId });
  }

  async selectPenaltyTaker(matchId: string, teamId: string, playerId: string) {
    return this.invoke<MatchCommandResult>('SelectPenaltyTaker', { matchId, teamId, playerId });
  }

  /**
   * Names the order a club takes a shootout in. It is a list for the same reason the
   * substitution is a pair: a shootout is not one decision, it is five of them, and the
   * order they are taken in is the manager's.
   */
  async nameShootoutOrder(matchId: string, teamId: string, takerIds: string[]) {
    return this.invoke<MatchCommandResult>('NameShootoutOrder', { matchId, teamId, takerIds });
  }

  /**
   * A hub command returns what the service decided. A refusal is an answer with a
   * reason, not a broken call, so it is handed back to the caller instead of being lost
   * or thrown at it.
   */
  private async invoke<T>(method: string, payload: unknown): Promise<T | null> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return null;
    }

    return (await this.connection.invoke(method, payload)) as T;
  }

  /**
   * Leaves the match and stops the connection, but only for the lease that asked for
   * it. A stale lease is ignored: the connection it would close belongs to a mount that
   * is still on screen. The listeners are left alone, because they belong to the
   * screen, which removes them in its own cleanup.
   */
  disconnect(lease: number): void {
    if (lease !== this.activeLease) {
      return;
    }

    this.activeLease = 0;
    const matchId = this.currentMatchId;
    const roundId = this.currentRoundId;

    this.serialize(async () => {
      if (this.activeLease !== 0) {
        // A new mount connected while this teardown was queued.
        return;
      }

      this.currentMatchId = null;
      // Don't clear currentRoundId - the round subscription should persist
      // across match disconnects (e.g., React Strict Mode double-mount)
      const connection = this.connection;
      if (!connection) {
        return;
      }

      this.connection = null;

      if (connection.state === signalR.HubConnectionState.Connected) {
        if (matchId) {
          try {
            await connection.invoke('LeaveMatch', matchId);
          } catch {
            // The connection may already be gone; stopping is what matters.
          }
        }
        // Don't leave the round group here - it will be re-used on re-mount
        // or cleaned up naturally when the connection stops
      }

      try {
        await connection.stop();
      } catch {
        // A connection that never finished negotiating throws on stop; nothing to do.
      }
    }).catch(() => undefined);
  }
}

export default new MatchHubClient();
