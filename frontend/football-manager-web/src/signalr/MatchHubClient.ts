import * as signalR from '@microsoft/signalr';
import { HUB_BASE_URL, MATCH_HUB_PATH } from '@/config/env';
import type { MatchEngineEventDto, MatchResult, MatchStateDto } from '../types';

type Listener<T> = (payload: T) => void;

/**
 * Thin client over MatchHub. The frontend never decides anything about the game: it
 * subscribes to a match, receives the snapshot plus the ordered event stream, and sends
 * commands back. The server owns the clock.
 */
class MatchHubClient {
  private connection: signalR.HubConnection | null = null;
  private currentMatchId: string | null = null;

  private stateListeners = new Set<Listener<MatchStateDto>>();
  private eventListeners = new Set<Listener<MatchEngineEventDto[]>>();
  private resultListeners = new Set<Listener<MatchResult>>();

  async connect(matchId: string): Promise<void> {
    this.currentMatchId = matchId;

    if (!this.connection) {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${HUB_BASE_URL}${MATCH_HUB_PATH}`, {
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
        })
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

      // After a reconnect the server has no group for us any more, so we subscribe
      // again and receive the current snapshot before the next event.
      this.connection.onreconnected(async () => {
        if (this.currentMatchId) {
          await this.subscribe(this.currentMatchId);
        }
      });
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

  onState(listener: Listener<MatchStateDto>): void {
    this.stateListeners.add(listener);
  }

  onEvent(listener: Listener<MatchEngineEventDto[]>): void {
    this.eventListeners.add(listener);
  }

  onResult(listener: Listener<MatchResult>): void {
    this.resultListeners.add(listener);
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

  async continueSecondHalf(matchId: string) {
    return this.invoke('ContinueSecondHalf', { matchId });
  }

  async makeSubstitution(matchId: string, teamId: string, playerOutId: string, playerInId: string) {
    return this.invoke('MakeSubstitution', { matchId, teamId, playerOutId, playerInId });
  }

  async selectPenaltyTaker(matchId: string, teamId: string, playerId: string) {
    return this.invoke('SelectPenaltyTaker', { matchId, teamId, playerId });
  }

  private async invoke(method: string, payload: unknown): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke(method, payload);
  }

  async disconnect(): Promise<void> {
    if (this.connection && this.currentMatchId) {
      try {
        await this.connection.invoke('LeaveMatch', this.currentMatchId);
      } catch {
        // The connection may already be gone; stopping is what matters.
      }
    }

    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }

    this.currentMatchId = null;
    this.stateListeners.clear();
    this.eventListeners.clear();
    this.resultListeners.clear();
  }
}

export default new MatchHubClient();
