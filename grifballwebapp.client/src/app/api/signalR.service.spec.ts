import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { HttpTransportType, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { SignalRService } from './signalR.service';
import { AuthSession } from '../authSession.service';
import { HUB_RETRY_MS } from './hubLifecycle';

/** The Teams hub's use of hubLifecycle; the lifecycle itself is hubLifecycle.spec.ts's. */
describe('SignalRService', () => {
  let connection: {
    start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy; off: jasmine.Spy; onreconnected: jasmine.Spy; onclose: jasmine.Spy;
    state: HubConnectionState;
  };
  let withUrl: jasmine.Spy;
  let service: SignalRService;
  const flush = () => new Promise(r => setTimeout(r));

  function handlerFor(event: string): (dto: unknown) => void {
    const call = connection.on.calls.all().find(c => c.args[0] === event);
    expect(call).withContext(`handler registered for ${event}`).toBeDefined();
    return call!.args[1];
  }

  beforeEach(() => {
    connection = {
      start: jasmine.createSpy('start').and.callFake(() => {
        connection.state = HubConnectionState.Connected;
        return Promise.resolve();
      }),
      stop: jasmine.createSpy('stop').and.callFake(() => {
        connection.state = HubConnectionState.Disconnected;
        return Promise.resolve();
      }),
      on: jasmine.createSpy('on'),
      off: jasmine.createSpy('off'),
      onreconnected: jasmine.createSpy('onreconnected'),
      onclose: jasmine.createSpy('onclose'),
      state: HubConnectionState.Disconnected,
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as any);
    spyOn(console, 'log');

    service = TestBed.inject(SignalRService);
  });

  it('connects to the teams hub and starts the connection immediately', async () => {
    expect(withUrl.calls.mostRecent().args[0]).toBe('hub/TeamsHub');
    expect(service.hubConnection as unknown).toBe(connection);
    await flush();
    expect(connection.start).toHaveBeenCalledTimes(1);
  });

  it('opens a WebSocket straight away, without negotiate (no sticky sessions needed with several backend pods)', () => {
    expect(withUrl.calls.mostRecent().args[1]).toEqual({ skipNegotiation: true, transport: HttpTransportType.WebSockets });
  });

  it('connects again when someone signs in or out', async () => {
    await flush();
    TestBed.inject(AuthSession).signedInOrOut();
    await flush();
    expect(connection.stop).toHaveBeenCalledTimes(1);
    expect(connection.start).toHaveBeenCalledTimes(2);
  });

  it('starts again once automatic reconnects give up (a rollout), instead of staying down until a reload', async () => {
    jasmine.clock().install();
    try {
      for (let i = 0; i < 10; i++) await Promise.resolve();
      const listener = jasmine.createSpy('connected');
      service.connected(listener);

      connection.state = HubConnectionState.Disconnected;
      connection.onclose.calls.mostRecent().args[0](new Error('gone'));
      jasmine.clock().tick(HUB_RETRY_MS[0]);
      for (let i = 0; i < 10; i++) await Promise.resolve();

      expect(connection.start).toHaveBeenCalledTimes(2);
      // The draft reloads: whatever was sent while it was down was missed
      expect(listener).toHaveBeenCalledTimes(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('stops the connection when the app goes', async () => {
    await flush();
    TestBed.resetTestingModule();
    expect(connection.stop).toHaveBeenCalledTimes(1);
  });

  describe('connected', () => {
    it('tells its listeners when the connection has started', async () => {
      const listener = jasmine.createSpy('connected');
      service.connected(listener);
      await flush();
      expect(listener).toHaveBeenCalledTimes(1);
    });

    it('tells them again on a reconnect, and on a restart as another user', async () => {
      const listener = jasmine.createSpy('connected');
      await flush();
      service.connected(listener);
      (connection.onreconnected.calls.mostRecent().args[0] as () => void)();
      expect(listener).toHaveBeenCalledTimes(1);

      TestBed.inject(AuthSession).signedInOrOut();
      await flush();
      expect(listener).toHaveBeenCalledTimes(2);
    });

    it('not when starting fails', async () => {
      connection.start.and.callFake(() => Promise.reject('offline'));
      const listener = jasmine.createSpy('connected');
      service.connected(listener);
      await flush();
      expect(listener).not.toHaveBeenCalled();
      expect(console.log).toHaveBeenCalledWith('Error while starting the Teams hub connection: offline');
    });

    it('returns a function that stops telling that listener', async () => {
      const listener = jasmine.createSpy('connected');
      service.connected(listener)();
      await flush();
      expect(listener).not.toHaveBeenCalled();
    });
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
      connection.stop.calls.reset();
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
