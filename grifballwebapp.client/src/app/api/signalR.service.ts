import { Injectable } from '@angular/core';
import type { CaptainPlacementDto, RemoveCaptainDto } from '@api/models';
import { HubConnection } from '@microsoft/signalr';
import { hubLifecycle } from './hubLifecycle';
import type { AddPlayerToTeamRequestDto } from '@api/models';
import type { MovePlayerToTeamRequestDto } from '@api/models';
import type { RemovePlayerFromTeamRequestDto } from '@api/models';


/** A captain added to the draft, as the hub announces it. */
export interface CaptainAddedDto {
  seasonID: number;
  personID: number;
  teamID: number;
  teamName: string;
  captainName: string;
  orderNumber: number;
}

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
