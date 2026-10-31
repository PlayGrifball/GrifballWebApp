import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CaptainAddedDto, CaptainPlacementDto, RemoveCaptainDto } from './dtos/captainPlacementDto';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { AuthSession } from '../authSession.service';
import { AddPlayerToTeamRequestDto } from './dtos/AddPlayerToTeamRequestDto';
import { MovePlayerToTeamRequestDto } from './dtos/MovePlayerToTeamRequestDto';
import { RemovePlayerFromTeamRequestDto } from './dtos/RemovePlayerFromTeamRequestDto';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  /**
   * The Teams hub. The browser's session cookie goes with the connection (same origin), so the hub
   * knows the user. Only in the browser: a page rendered on the server has no connection, and its
   * handlers are registered on one that is never started.
   */
  public hubConnection: HubConnection = new HubConnectionBuilder()
    .withUrl(isPlatformBrowser(inject(PLATFORM_ID)) ? 'hub/TeamsHub' : 'http://localhost/hub/TeamsHub')
    .withAutomaticReconnect()
    .build();

  /** Called each time the connection comes up (see connected). */
  private connectedListeners?: Set<() => void>;

  /**
   * The connection's starts and restarts, one after another: a start while another is still under way
   * throws ("Cannot start a HubConnection that is not in the 'Disconnected' state").
   */
  private lifecycle: Promise<void> = Promise.resolve();
  /** A restart is queued and has not begun: another sign-in or out meanwhile needs no second one. */
  private restartQueued = false;

  constructor() {
    if (!isPlatformBrowser(inject(PLATFORM_ID)))
      return;

    // Back after a drop: whatever was sent meanwhile was missed
    this.hubConnection.onreconnected(() => this.notifyConnected());
    this.lifecycle = this.startConnection();

    // The connection's user is fixed when it connects: connect again as whoever signed in (or out),
    // once any start or restart under way is done.
    inject(AuthSession).changed$.subscribe(() => {
      if (this.restartQueued)
        return;
      this.restartQueued = true;
      this.queue(() => {
        this.restartQueued = false;
        return this.hubConnection.stop()
          .then(() => this.startConnection())
          .catch(err => console.log('Error while trying to restart the connection: ' + err));
      });
    });
  }

  private queue(step: () => Promise<void>): void {
    this.lifecycle = this.lifecycle.then(step);
  }

  private startConnection = (): Promise<void> => {
    return this.hubConnection
      .start()
      .then(() => {
        console.log('Connection started');
        this.notifyConnected();
      })
      .catch(err => console.log('Error while starting connection: ' + err));
  }

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

  /**
   * Registers `callback` for a hub method. Returns a function that unregisters exactly this
   * handler; components call it when they are destroyed so handlers don't pile up across visits.
   */
  private on<T>(methodName: string, callback: (value: T) => void): () => void {
    const handler = (dto: T) => callback.call(this, dto);
    this.hubConnection.on(methodName, handler);
    return () => this.hubConnection.off(methodName, handler);
  }

  public addCaptain(callback: (value: CaptainAddedDto) => void): () => void {
    return this.on('AddCaptain', callback);
  }

  public resortCaptain(callback: (value: CaptainPlacementDto) => void): () => void {
    return this.on('ResortCaptain', callback);
  }

  public removeCaptain(callback: (value: RemoveCaptainDto) => void): () => void {
    return this.on('RemoveCaptain', callback);
  }

  public removePlayerFromTeam(callback: (value: RemovePlayerFromTeamRequestDto) => void): () => void {
    return this.on('RemovePlayerFromTeam', callback);
  }

  public movePlayerToTeam(callback: (value: MovePlayerToTeamRequestDto) => void): () => void {
    return this.on('MovePlayerToTeam', callback);
  }

  public addPlayerToTeam(callback: (value: AddPlayerToTeamRequestDto) => void): () => void {
    return this.on('AddPlayerToTeam', callback);
  }

  public lockCaptains(callback: (value: number) => void): () => void {
    return this.on('LockCaptains', callback);
  }

  public unlockCaptains(callback: (value: number) => void): () => void {
    return this.on('UnlockCaptains', callback);
  }
}
