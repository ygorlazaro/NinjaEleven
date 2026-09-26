import * as signalR from '@microsoft/signalr';
import type { MatchEngineEventDto, MatchStateDto } from '../types';

class MatchHubClient {
  private connection: signalR.HubConnection | null = null;
  private callbacks: Map<string, (payload: any) => void> = new Map();

  async connect(matchId: string): Promise<void> {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`/matchHub?matchId=${matchId}`, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('MatchEvent', (event: MatchEngineEventDto) => {
      this.callbacks.get('matchEvent')?.(event);
    });

    this.connection.on('MatchState', (state: MatchStateDto) => {
      this.callbacks.get('matchState')?.(state);
    });

    this.connection.on('MatchFinished', (result: any) => {
      this.callbacks.get('matchFinished')?.(result);
    });

    await this.connection.start();
  }

  on(event: string, callback: (payload: any) => void): void {
    this.callbacks.set(event, callback);
  }

  off(event: string): void {
    this.callbacks.delete(event);
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
    this.callbacks.clear();
  }

  async sendCommand(method: string, ...args: any[]): Promise<void> {
    if (this.connection) {
      await this.connection.invoke(method, ...args);
    }
  }
}

export default new MatchHubClient();
