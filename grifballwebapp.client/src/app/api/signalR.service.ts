import { Injectable, effect, inject } from '@angular/core';
import { CaptainAddedDto, CaptainPlacementDto, RemoveCaptainDto } from './dtos/captainPlacementDto';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { AccountService } from '../account.service';
import { AddPlayerToTeamRequestDto } from './dtos/AddPlayerToTeamRequestDto';
import { MovePlayerToTeamRequestDto } from './dtos/MovePlayerToTeamRequestDto';
import { RemovePlayerFromTeamRequestDto } from './dtos/RemovePlayerFromTeamRequestDto';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  constructor() {
    const accountService = inject(AccountService);

    this.hubConnection = new HubConnectionBuilder()
      .withUrl('hub/TeamsHub', {
        accessTokenFactory: () => accountService.accessToken() ?? ""
      })
      .withAutomaticReconnect()
      .build();

    console.log('starting connection');
    this.startConnection();
    console.log('finished connection');

    effect(() => {
      const accessToken = accountService.accessToken();
      console.log("SignalR servie access token change");
      this.hubConnection.stop()
        .then(() => this.startConnection())
        .catch(err => console.log('Error while trying to restart the connection: ' + err));
    });
  }

  public hubConnection: HubConnection;

  private startConnection = () => {
    this.hubConnection
      .start()
      .then(() => console.log('Connection started'))
      .catch(err => console.log('Error while starting connection: ' + err));
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
