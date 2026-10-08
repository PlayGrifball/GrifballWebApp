import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { SignalRService } from './signalR.service';
import { AccountService } from '../account.service';

describe('SignalRService', () => {
  let connection: { start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy };
  let accessToken: ReturnType<typeof signal<string | undefined>>;
  let withUrl: jasmine.Spy;
  let service: SignalRService;

  function handlerFor(event: string): (dto: unknown) => void {
    const call = connection.on.calls.all().find(c => c.args[0] === event);
    expect(call).withContext(`handler registered for ${event}`).toBeDefined();
    return call!.args[1];
  }

  beforeEach(() => {
    connection = {
      start: jasmine.createSpy('start').and.returnValue(Promise.resolve()),
      stop: jasmine.createSpy('stop').and.returnValue(Promise.resolve()),
      on: jasmine.createSpy('on'),
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as any);
    spyOn(console, 'log');

    accessToken = signal<string | undefined>('token-1');
    TestBed.configureTestingModule({
      providers: [{ provide: AccountService, useValue: { accessToken } }]
    });
    service = TestBed.inject(SignalRService);
  });

  it('connects to the teams hub and starts the connection immediately', () => {
    expect(withUrl.calls.mostRecent().args[0]).toBe('hub/TeamsHub');
    expect(service.hubConnection as unknown).toBe(connection);
    expect(connection.start).toHaveBeenCalledTimes(1);
  });

  it('supplies the current access token, or an empty string when logged out', () => {
    const options = withUrl.calls.mostRecent().args[1];
    expect(options.accessTokenFactory()).toBe('token-1');

    accessToken.set(undefined);
    expect(options.accessTokenFactory()).toBe('');
  });

  it('restarts the connection when the access token changes', async () => {
    TestBed.tick();
    await Promise.resolve();
    await Promise.resolve();
    expect(connection.stop).toHaveBeenCalledTimes(1);
    expect(connection.start).toHaveBeenCalledTimes(2);

    accessToken.set('token-2');
    TestBed.tick();
    await Promise.resolve();
    await Promise.resolve();
    expect(connection.stop).toHaveBeenCalledTimes(2);
    expect(connection.start).toHaveBeenCalledTimes(3);
  });

  it('logs instead of throwing when restarting fails', async () => {
    connection.stop.and.callFake(() => Promise.reject('stuck'));

    accessToken.set('token-3');
    TestBed.tick();
    await new Promise(r => setTimeout(r));

    expect(console.log).toHaveBeenCalledWith('Error while trying to restart the connection: stuck');
  });

  it('logs when the initial start fails', async () => {
    connection.start.and.callFake(() => Promise.reject('offline'));
    (service as any).startConnection();
    await new Promise(r => setTimeout(r));

    expect(console.log).toHaveBeenCalledWith('Error while starting connection: offline');
  });

  const events: [keyof SignalRService, string, unknown][] = [
    ['addCaptain', 'AddCaptain', { captainID: 1 }],
    ['resortCaptain', 'ResortCaptain', { captainID: 2 }],
    ['removeCaptain', 'RemoveCaptain', { captainID: 3 }],
    ['removePlayerFromTeam', 'RemovePlayerFromTeam', { personID: 4 }],
    ['movePlayerToTeam', 'MovePlayerToTeam', { personID: 5 }],
    ['addPlayerToTeam', 'AddPlayerToTeam', { personID: 6 }],
    ['lockCaptains', 'LockCaptains', 7],
    ['unlockCaptains', 'UnlockCaptains', 8],
  ];

  for (const [method, event, payload] of events) {
    it(`${String(method)} forwards ${event} hub messages to the callback`, () => {
      const callback = jasmine.createSpy(event);
      (service[method] as (cb: (v: unknown) => void) => void).call(service, callback);

      handlerFor(event)(payload);

      expect(callback).toHaveBeenCalledOnceWith(payload);
      expect(callback.calls.mostRecent().object).toBe(service);
    });
  }
});
