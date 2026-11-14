import { TestBed } from '@angular/core/testing';
import { EnvironmentInjector, PLATFORM_ID, createEnvironmentInjector, runInInjectionContext } from '@angular/core';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { AuthSession } from '../auth/authSession.service';
import { HUB_RETRY_MS, hubLifecycle } from './hubLifecycle';

describe('hubLifecycle', () => {
  let connection: {
    start: jasmine.Spy; stop: jasmine.Spy; onreconnected: jasmine.Spy; onclose: jasmine.Spy;
    state: HubConnectionState;
  };
  let withUrl: jasmine.Spy;
  let withAutomaticReconnect: jasmine.Spy;
  let log: jasmine.Spy;
  let connected: jasmine.Spy;
  let injector: EnvironmentInjector;

  /** Lets the lifecycle's promise callbacks run (the jasmine clock leaves no setTimeout to wait on). */
  const settled = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
  const reconnectsGaveUp = () => {
    connection.state = HubConnectionState.Disconnected;
    connection.onclose.calls.mostRecent().args[0](new Error('gone'));
  };

  function create(): void {
    injector = createEnvironmentInjector([], TestBed.inject(EnvironmentInjector));
    runInInjectionContext(injector, () => hubLifecycle('hub/TestHub', { name: 'Test hub', connected }));
  }

  beforeEach(() => {
    jasmine.clock().install();
    connection = {
      // Like the real one: up once started, down once stopped
      start: jasmine.createSpy('start').and.callFake(() => {
        connection.state = HubConnectionState.Connected;
        return Promise.resolve();
      }),
      stop: jasmine.createSpy('stop').and.callFake(() => {
        const wasUp = connection.state !== HubConnectionState.Disconnected;
        connection.state = HubConnectionState.Disconnected;
        if (wasUp)
          connection.onclose.calls.mostRecent().args[0]();
        return Promise.resolve();
      }),
      onreconnected: jasmine.createSpy('onreconnected'),
      onclose: jasmine.createSpy('onclose'),
      state: HubConnectionState.Disconnected,
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    withAutomaticReconnect = spyOn(HubConnectionBuilder.prototype, 'withAutomaticReconnect').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as unknown as HubConnection);
    log = spyOn(console, 'log');
    connected = jasmine.createSpy('connected');
  });

  afterEach(() => jasmine.clock().uninstall());

  it('negotiates with every transport, WebSockets first, with automatic reconnects, and starts it', async () => {
    create();
    expect(withUrl).toHaveBeenCalledOnceWith('hub/TestHub', { transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling });
    // No token either: the session cookie goes with the connection
    expect(withUrl.calls.mostRecent().args[1].accessTokenFactory).toBeUndefined();
    expect(withAutomaticReconnect).toHaveBeenCalled();
    await settled();
    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(connected).toHaveBeenCalledTimes(1);
  });

  it('says it is connected again after a reconnect', async () => {
    create();
    await settled();
    connection.onreconnected.calls.mostRecent().args[0]();
    expect(connected).toHaveBeenCalledTimes(2);
  });

  describe('when a start fails', () => {
    beforeEach(() => {
      connection.start.and.callFake(() => Promise.reject(new Error('refused')));
    });

    it('logs it, and tries again with a growing backoff that the last delay caps', async () => {
      create();
      await settled();
      expect(log).toHaveBeenCalledWith('Error while starting the Test hub connection: Error: refused');
      expect(connected).not.toHaveBeenCalled();

      for (const [attempt, delay] of [...HUB_RETRY_MS, HUB_RETRY_MS[HUB_RETRY_MS.length - 1]].entries()) {
        jasmine.clock().tick(delay - 1);
        await settled();
        expect(connection.start).toHaveBeenCalledTimes(attempt + 1);
        jasmine.clock().tick(1);
        await settled();
        expect(connection.start).toHaveBeenCalledTimes(attempt + 2);
      }
    });

    it('starts the backoff over once connected', async () => {
      create();
      await settled();
      jasmine.clock().tick(HUB_RETRY_MS[0]);
      await settled();
      connection.start.and.callFake(() => {
        connection.state = HubConnectionState.Connected;
        return Promise.resolve();
      });
      jasmine.clock().tick(HUB_RETRY_MS[1]);
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(3);
      expect(connected).toHaveBeenCalledTimes(1);

      reconnectsGaveUp();
      jasmine.clock().tick(HUB_RETRY_MS[0]);
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(4);
    });
  });

  it('starts it again with a backoff once automatic reconnects give up (a rollout), and says it is connected', async () => {
    create();
    await settled();

    reconnectsGaveUp();
    jasmine.clock().tick(HUB_RETRY_MS[0] - 1);
    await settled();
    expect(connection.start).toHaveBeenCalledTimes(1);
    jasmine.clock().tick(1);
    await settled();
    expect(connection.start).toHaveBeenCalledTimes(2);
    expect(connected).toHaveBeenCalledTimes(2);
  });

  it('does not start one that is already on its way', async () => {
    create();
    await settled();

    connection.onclose.calls.mostRecent().args[0]();
    connection.state = HubConnectionState.Connecting;
    jasmine.clock().tick(HUB_RETRY_MS[0]);
    await settled();
    expect(connection.start).toHaveBeenCalledTimes(1);
  });

  describe('on a sign-in or out', () => {
    it('connects again at once, as the new user, and says it is connected', async () => {
      create();
      await settled();

      TestBed.inject(AuthSession).signedInOrOut();
      await settled();
      expect(connection.stop).toHaveBeenCalledTimes(1);
      expect(connection.start).toHaveBeenCalledTimes(2);
      expect(connected).toHaveBeenCalledTimes(2);

      // That stop's close was the restart's own: no retry on top of it
      jasmine.clock().tick(HUB_RETRY_MS[HUB_RETRY_MS.length - 1]);
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(2);
    });

    it('connects at once when a retry is waiting, and that retry does not start it again', async () => {
      connection.start.and.callFake(() => Promise.reject(new Error('refused')));
      create();
      await settled();
      connection.start.and.callFake(() => {
        connection.state = HubConnectionState.Connected;
        return Promise.resolve();
      });

      TestBed.inject(AuthSession).signedInOrOut();
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(2);
      expect(connected).toHaveBeenCalledTimes(1);

      jasmine.clock().tick(HUB_RETRY_MS[HUB_RETRY_MS.length - 1]);
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(2);
    });

    it('restarts one step at a time: never a start while another is under way', async () => {
      // A connection that, like the real one, refuses a start unless it is disconnected
      let finishStart!: () => void;
      const overlapping: HubConnectionState[] = [];
      connection.start.and.callFake(() => {
        if (connection.state !== HubConnectionState.Disconnected) {
          overlapping.push(connection.state);
          return Promise.reject(new Error('Cannot start a HubConnection that is not in the Disconnected state'));
        }
        connection.state = HubConnectionState.Connecting;
        return new Promise<void>(resolve => finishStart = () => {
          connection.state = HubConnectionState.Connected;
          resolve();
        });
      });
      create();
      await settled();
      finishStart();
      await settled();

      const session = TestBed.inject(AuthSession);
      session.signedInOrOut();
      session.signedInOrOut();
      session.signedInOrOut();
      await settled();
      // Three at once: one restart covers them all
      expect(connection.stop).toHaveBeenCalledTimes(1);
      expect(connection.start).toHaveBeenCalledTimes(2);

      // A sign-out while that restart is still connecting: queued behind it, not run alongside
      session.signedInOrOut();
      await settled();
      expect(connection.stop).toHaveBeenCalledTimes(1);

      finishStart();
      await settled();
      expect(connection.stop).toHaveBeenCalledTimes(2);
      expect(connection.start).toHaveBeenCalledTimes(3);
      expect(overlapping).toEqual([]);
    });

    it('logs instead of throwing when restarting fails', async () => {
      create();
      await settled();
      connection.stop.and.callFake(() => Promise.reject('stuck'));

      TestBed.inject(AuthSession).signedInOrOut();
      await settled();
      expect(log).toHaveBeenCalledWith('Error while restarting the Test hub connection: stuck');
    });
  });

  describe('once its injector is destroyed', () => {
    it('stops it, and neither retries nor restarts', async () => {
      create();
      await settled();

      injector.destroy();
      expect(connection.stop).toHaveBeenCalledTimes(1);

      connection.onclose.calls.mostRecent().args[0]();
      TestBed.inject(AuthSession).signedInOrOut();
      jasmine.clock().tick(HUB_RETRY_MS[HUB_RETRY_MS.length - 1]);
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(1);
      expect(connection.stop).toHaveBeenCalledTimes(1);
    });

    it('does not try again a start that fails after that', async () => {
      let fail!: (err: Error) => void;
      connection.start.and.callFake(() => new Promise<void>((_, reject) => fail = reject));
      create();
      await settled();
      injector.destroy();
      fail(new Error('refused'));
      await settled();

      jasmine.clock().tick(HUB_RETRY_MS[HUB_RETRY_MS.length - 1]);
      await settled();
      expect(connection.start).toHaveBeenCalledTimes(1);
    });
  });

  describe('on the server', () => {
    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: 'server' }] });
    });

    it('builds it with an absolute URL (SignalR cannot resolve a relative one there), but never connects', async () => {
      create();
      TestBed.inject(AuthSession).signedInOrOut();
      await settled();
      expect(withUrl.calls.mostRecent().args[0]).toBe('http://localhost/hub/TestHub');
      expect(connection.start).not.toHaveBeenCalled();
      expect(connection.stop).not.toHaveBeenCalled();
      expect(connection.onclose).not.toHaveBeenCalled();
    });
  });
});
