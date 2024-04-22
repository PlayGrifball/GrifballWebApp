import { Injectable, effect } from '@angular/core';
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
  constructor(accountService: AccountService) {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl('api/TeamsHub', {
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

  public addCaptain(callback: (value: CaptainAddedDto) => void): void {
    this.hubConnection.on('AddCaptain', (dto: CaptainAddedDto) => {
      callback.call(this, dto);
    });
  }

  public resortCaptain(callback: (value: CaptainPlacementDto) => void): void {
    this.hubConnection.on('ResortCaptain', (dto: CaptainPlacementDto) => {
      callback.call(this, dto);
    });
  }

  public removeCaptain(callback: (value: RemoveCaptainDto) => void): void {
    this.hubConnection.on('RemoveCaptain', (dto: RemoveCaptainDto) => {
      callback.call(this, dto);
    });
  }

  public removePlayerFromTeam(callback: (value: RemovePlayerFromTeamRequestDto) => void): void {
    this.hubConnection.on('RemovePlayerFromTeam', (dto: RemovePlayerFromTeamRequestDto) => {
      callback.call(this, dto);
    });
  }

  public movePlayerToTeam(callback: (value: MovePlayerToTeamRequestDto) => void): void {
    this.hubConnection.on('MovePlayerToTeam', (dto: MovePlayerToTeamRequestDto) => {
      callback.call(this, dto);
    });
  }

  public addPlayerToTeam(callback: (value: AddPlayerToTeamRequestDto) => void): void {
    this.hubConnection.on('AddPlayerToTeam', (dto: AddPlayerToTeamRequestDto) => {
      callback.call(this, dto);
    });
  }

  public lockCaptains(callback: (value: number) => void): void {
    this.hubConnection.on('LockCaptains', (dto: number) => {
      callback.call(this, dto);
    });
  }

  public unlockCaptains(callback: (value: number) => void): void {
    this.hubConnection.on('UnlockCaptains', (dto: number) => {
      callback.call(this, dto);
    });
  }

}
