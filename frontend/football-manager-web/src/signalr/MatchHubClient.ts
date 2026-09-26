import * as signalR from '@microsoft/signalr';
import { HUB_BASE_URL, MATCH_HUB_PATH } from '@/config/env';
import type {
  MatchCommandResult,
  MatchEngineEventDto,
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

      // After a reconnect the server has no group for us any more, so we subscribe
      // again and receive the current snapshot before the next event.
      this.connection.onreconnected(async () => {
        if (this.currentMatchId) {
          await this.subscribe(this.currentMatchId);
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
   */
  async subscribeMatchday(roundId: string): Promise<void> {
    if (roundId) {
      await this.invoke('SubscribeMatchday', { roundId });
    }
  }

  async continueSecondHalf(matchId: string) {
    return this.invoke('ContinueSecondHalf', { matchId });
  }

  async makeSubstitution(matchId: string, teamId: string, playerOutId: string, playerInId: string) {
    return this.invoke('MakeSubstitution', { matchId, teamId, playerOutId, playerInId });
  }

  async selectPenaltyTaker(matchId: string, teamId: string, playerId: string) {
    return this.invoke<MatchCommandResult>('SelectPenaltyTaker', { matchId, teamId, playerId });
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

    this.serialize(async () => {
      if (this.activeLease !== 0) {
        // A new mount connected while this teardown was queued.
        return;
      }

      this.currentMatchId = null;
      const connection = this.connection;
      if (!connection) {
        return;
      }

      this.connection = null;

      if (connection.state === signalR.HubConnectionState.Connected && matchId) {
        try {
          await connection.invoke('LeaveMatch', matchId);
        } catch {
          // The connection may already be gone; stopping is what matters.
        }
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
