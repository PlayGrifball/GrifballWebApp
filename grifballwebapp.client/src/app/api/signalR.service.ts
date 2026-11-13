import { Injectable } from '@angular/core';
import type {
  AddPlayerToTeamRequestDto, CaptainAddedDto, CaptainPlacementDto, MovePlayerToTeamRequestDto, RemoveCaptainDto, RemovePlayerFromTeamRequestDto,
} from '@api/models';
import { getReceiverRegister } from '@signalr/TypedSignalR.Client';
import type { ITeamsHubClient } from '@signalr/TypedSignalR.Client/GrifballWebApp.Server.Teams';
import { HubConnection } from '@microsoft/signalr';
import { hubLifecycle } from './hubLifecycle';

/** A message the Teams hub sends, by its receiver method's name. */
type Message = keyof ITeamsHubClient;
/** What a message carries. */
type Payload<M extends Message> = Parameters<ITeamsHubClient[M]>[0];

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  /**
   * The Teams hub. The browser's session cookie goes with the connection (same origin), so the hub
   * knows the user. Started, retried, and made again on a sign-in or out by hubLifecycle; only in
   * the browser: a page rendered on the server registers its handlers on one that is never started.
   */
  public hubConnection: HubConnection = hubLifecycle('hub/TeamsHub', {
    name: 'Teams hub',
    connected: () => this.notifyConnected(),
  });

  /** Called each time the connection comes up (see connected). */
  private connectedListeners?: Set<() => void>;

  /**
   * Calls `callback` each time the connection comes up: when it starts (also again, as another
   * user) and when it reconnects. Not for a connection that is already up. Anything sent while it
   * was down was missed, so pages that show hub updates reload then. Returns the unregister function.
   */
  public connected(callback: () => void): () => void {
    const listeners = this.connectedListeners ??= new Set();
    listeners.add(callback);
    return () => listeners.delete(callback);
  }

  private notifyConnected(): void {
    this.connectedListeners?.forEach(listener => listener());
  }

  /** Each hub message's callbacks, by its receiver method. */
  private readonly listeners = new Map<Message, Set<(value: never) => void>>();

  /**
   * The hub's receiver, generated from its C# interface (ITeamsHubClient): every message, by its name on the wire, to
   * that message's callbacks. Registered once, on the connection that is kept (started again on a sign-in or out).
   */
  constructor() {
    getReceiverRegister('ITeamsHubClient').register(this.hubConnection, {
      addCaptain: this.dispatch('addCaptain'),
      resortCaptain: this.dispatch('resortCaptain'),
      removeCaptain: this.dispatch('removeCaptain'),
      removePlayerFromTeam: this.dispatch('removePlayerFromTeam'),
      movePlayerToTeam: this.dispatch('movePlayerToTeam'),
      addPlayerToTeam: this.dispatch('addPlayerToTeam'),
      lockCaptains: this.dispatch('lockCaptains'),
      unlockCaptains: this.dispatch('unlockCaptains'),
    });
  }

  private dispatch<M extends Message>(message: M): (value: Payload<M>) => Promise<void> {
    return value => {
      this.listeners.get(message)?.forEach(listener => (listener as (value: Payload<M>) => void).call(this, value));
      return Promise.resolve();
    };
  }

  /**
   * Registers `callback` for a hub message. Returns a function that unregisters exactly this
   * callback; components call it when they are destroyed so callbacks don't pile up across visits.
   */
  private on<M extends Message>(message: M, callback: (value: Payload<M>) => void): () => void {
    const listeners = this.listeners.get(message) ?? new Set();
    this.listeners.set(message, listeners);
    const listener = callback as (value: never) => void;
    listeners.add(listener);
    return () => listeners.delete(listener);
  }

  public addCaptain(callback: (value: CaptainAddedDto) => void): () => void {
    return this.on('addCaptain', callback);
  }

  public resortCaptain(callback: (value: CaptainPlacementDto) => void): () => void {
    return this.on('resortCaptain', callback);
  }

  public removeCaptain(callback: (value: RemoveCaptainDto) => void): () => void {
    return this.on('removeCaptain', callback);
  }

  public removePlayerFromTeam(callback: (value: RemovePlayerFromTeamRequestDto) => void): () => void {
    return this.on('removePlayerFromTeam', callback);
  }

  public movePlayerToTeam(callback: (value: MovePlayerToTeamRequestDto) => void): () => void {
    return this.on('movePlayerToTeam', callback);
  }

  public addPlayerToTeam(callback: (value: AddPlayerToTeamRequestDto) => void): () => void {
    return this.on('addPlayerToTeam', callback);
  }

  public lockCaptains(callback: (value: number) => void): () => void {
    return this.on('lockCaptains', callback);
  }

  public unlockCaptains(callback: (value: number) => void): () => void {
    return this.on('unlockCaptains', callback);
  }
}
