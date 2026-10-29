import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { SignalRService } from './signalR.service';
import { AuthSession } from '../authSession.service';

describe('SignalRService', () => {
  let connection: { start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy; off: jasmine.Spy };
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
      off: jasmine.createSpy('off'),
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as any);
    spyOn(console, 'log');

    service = TestBed.inject(SignalRService);
  });

  it('connects to the teams hub and starts the connection immediately', () => {
    expect(withUrl.calls.mostRecent().args[0]).toBe('hub/TeamsHub');
    expect(service.hubConnection as unknown).toBe(connection);
    expect(connection.start).toHaveBeenCalledTimes(1);
  });

  it('sends no token: the session cookie goes with the connection', () => {
    expect(withUrl.calls.mostRecent().args[1]).toBeUndefined();
  });

  it('connects again when someone signs in or out', async () => {
    TestBed.inject(AuthSession).signedInOrOut();
    await Promise.resolve();
    await Promise.resolve();
    expect(connection.stop).toHaveBeenCalledTimes(1);
    expect(connection.start).toHaveBeenCalledTimes(2);
  });

  it('logs instead of throwing when restarting fails', async () => {
    connection.stop.and.callFake(() => Promise.reject('stuck'));

    TestBed.inject(AuthSession).signedInOrOut();
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
    ['addCaptain', 'AddCaptain', { seasonID: 1, personID: 11, teamName: 'Reds', captainName: 'Red', orderNumber: 1 }],
    ['resortCaptain', 'ResortCaptain', { seasonID: 1, personID: 12, orderNumber: 2 }],
    ['removeCaptain', 'RemoveCaptain', { seasonID: 1, personID: 13 }],
    ['removePlayerFromTeam', 'RemovePlayerFromTeam', { seasonID: 1, captainID: 11, personID: 4 }],
    ['movePlayerToTeam', 'MovePlayerToTeam', { seasonID: 1, previousCaptainID: 11, newCaptainID: 12, personID: 5, roundNumber: 1 }],
    ['addPlayerToTeam', 'AddPlayerToTeam', { seasonID: 1, captainID: 11, personID: 6 }],
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

    it(`${String(method)} returns a function that unregisters that same ${event} handler`, () => {
      const unsubscribe = (service[method] as (cb: (v: unknown) => void) => () => void).call(service, () => {});

      unsubscribe();

      expect(connection.off).toHaveBeenCalledOnceWith(event, handlerFor(event));
    });
  }

  describe('on the server', () => {
    beforeEach(() => {
      TestBed.resetTestingModule();
      connection.start.calls.reset();
      TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: 'server' }] });
      service = TestBed.inject(SignalRService);
    });

    it('never connects, and never restarts on a sign-in', async () => {
      TestBed.inject(AuthSession).signedInOrOut();
      await Promise.resolve();
      expect(connection.start).not.toHaveBeenCalled();
      expect(connection.stop).not.toHaveBeenCalled();
      // An absolute URL: SignalR cannot resolve a relative one outside a browser
      expect(withUrl.calls.mostRecent().args[0]).toBe('http://localhost/hub/TeamsHub');
    });

    it('still takes handlers (the team builder registers them while rendering)', () => {
      service.lockCaptains(() => {})();
      expect(connection.on).toHaveBeenCalledWith('LockCaptains', jasmine.any(Function));
    });
  });
});
