import { TestBed } from '@angular/core/testing';
import {
  HttpClient, HttpRequest, HttpResponse, HubConnectionBuilder, HubConnectionState, IHttpConnectionOptions,
} from '@microsoft/signalr';
import { SignalRService } from './signalR.service';

/** The record separator ending every JSON hub protocol message. */
const RS = '\x1e';

/**
 * What the server side of ASP.NET Core SignalR says to the client over a WebSocket or long polling: the JSON hub
 * protocol (handshake, invocations), and for long polling the HTTP mechanics around it (a connect poll, then a
 * background one held until there is data, and the handshake and invocations the client sends as POSTs). It runs the
 * real @microsoft/signalr client through SignalRService, with the service's own options, so a client upgrade that
 * changes the wire protocol or how the options are used fails here. The other spec (signalR.service.spec.ts) replaces
 * the connection with a stub.
 */
class FakeTeamsHub {
  readonly httpRequests: HttpRequest[] = [];
  readonly sockets: FakeSocket[] = [];
  readonly handshakes: unknown[] = [];
  readonly fromClient: { type: number }[] = [];

  /** The transports the negotiate offers, as the server's would (WebSockets first). */
  constructor(readonly offered: string[] = ['WebSockets', 'ServerSentEvents', 'LongPolling']) {}

  /**
   * Every HTTP request the client makes, recorded: the negotiate (answered with a connection and the offered
   * transports); over long polling, the connect poll (answered at once, empty, as the server's does), the
   * background poll (held until deliver sends it data, or the client stops it) and the handshake, then
   * invocations, sent as POSTs (answered with 204, their data delivered over the next background poll, as the
   * real server does).
   */
  readonly http = new (class extends HttpClient {
    private polls = 0;
    private pendingPoll: ((response: HttpResponse) => void) | null = null;
    private queued: string | null = null;
    private handshaken = false;
    constructor(private readonly hub: FakeTeamsHub) { super(); }
    override send(request: HttpRequest): Promise<HttpResponse> {
      this.hub.httpRequests.push(request);
      if (request.url?.includes('/negotiate'))
        return Promise.resolve(new HttpResponse(200, 'OK', JSON.stringify({
          negotiateVersion: 1, connectionId: 'connection', connectionToken: 'token',
          availableTransports: this.hub.offered.map(transport => ({ transport, transferFormats: ['Text', 'Binary'] })),
        })));
      if (request.method === 'POST') {
        // The handshake, then invocations: no WebSocket to send them on over long polling.
        for (const frame of String(request.content ?? '').split(RS).filter(f => f.length > 0)) {
          if (!this.handshaken) {
            this.handshaken = true;
            this.hub.handshakes.push(JSON.parse(frame));
            this.deliver('{}' + RS);
          } else {
            this.hub.fromClient.push(JSON.parse(frame));
          }
        }
        return Promise.resolve(new HttpResponse(204, 'No Content', ''));
      }
      if (request.method !== 'GET')
        // The DELETE the client sends on stop: nothing needs it answered.
        return Promise.resolve(new HttpResponse(200, 'OK', ''));
      if (this.polls++ === 0)
        // The connect poll: the server only confirms the transport works, with no data.
        return Promise.resolve(new HttpResponse(200, 'OK', ''));
      // The background poll: held until deliver sends data, or the client stops it.
      return new Promise(resolve => {
        if (this.queued !== null) {
          const data = this.queued;
          this.queued = null;
          resolve(new HttpResponse(200, 'OK', data));
          return;
        }
        this.pendingPoll = resolve;
        if (request.abortSignal)
          request.abortSignal.onabort = () => { this.pendingPoll = null; resolve(new HttpResponse(204, 'No Content', '')); };
      });
    }

    /** Resolves the open background poll with data, as the server would; queued if none is open yet. */
    deliver(data: string): void {
      if (this.pendingPoll) {
        const resolve = this.pendingPoll;
        this.pendingPoll = null;
        resolve(new HttpResponse(200, 'OK', data));
      } else {
        this.queued = data;
      }
    }
  })(this);

  /** The WebSocket constructor the client is given: each socket opens on the next turn, like a real one. */
  readonly WebSocket = (() => {
    const hub = this;
    return class extends FakeSocket {
      constructor(url: string) {
        super(hub, url);
      }
    };
  })();

  get latest(): FakeSocket | undefined {
    return this.sockets[this.sockets.length - 1];
  }

  /** Sends a hub invocation (the JSON hub protocol's type 1) down the newest socket, or the open long poll. */
  invoke(target: string, ...args: unknown[]): void {
    const frame = JSON.stringify({ type: 1, target, arguments: args }) + RS;
    if (this.latest) this.latest.deliver(frame);
    else this.http.deliver(frame);
  }
}

class FakeSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;

  readyState = FakeSocket.CONNECTING;
  binaryType = 'blob';
  handshaken = false;
  onopen: ((event: unknown) => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: ((event: { code: number, reason: string, wasClean: boolean }) => void) | null = null;
  onerror: ((event: unknown) => void) | null = null;

  constructor(private readonly hub: FakeTeamsHub, readonly url: string) {
    hub.sockets.push(this);
    setTimeout(() => {
      this.readyState = FakeSocket.OPEN;
      this.onopen?.({});
    });
  }

  send(data: string): void {
    for (const frame of data.split(RS).filter(f => f.length > 0)) {
      if (!this.handshaken) {
        this.hub.handshakes.push(JSON.parse(frame));
        this.handshaken = true;
        this.deliver('{}' + RS);
      } else {
        this.hub.fromClient.push(JSON.parse(frame));
      }
    }
  }

  close(code = 1000, reason = ''): void {
    if (this.readyState === FakeSocket.CLOSED)
      return;
    this.readyState = FakeSocket.CLOSED;
    setTimeout(() => this.onclose?.({ code, reason, wasClean: true }));
  }

  deliver(data: string): void {
    setTimeout(() => this.onmessage?.({ data }));
  }
}

async function waitFor(condition: () => boolean, what: string, timeoutMs = 5000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (!condition()) {
    if (Date.now() > deadline) {
      throw new Error(`Timed out waiting for ${what}`);
    }
    await new Promise(r => setTimeout(r, 10));
  }
}

describe('SignalRService over the SignalR wire protocol', () => {
  let hub: FakeTeamsHub;
  let service: SignalRService;

  beforeEach(() => {
    hub = new FakeTeamsHub();
    const withUrl = HubConnectionBuilder.prototype.withUrl as (this: HubConnectionBuilder, url: string, options: IHttpConnectionOptions) => HubConnectionBuilder;
    spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callFake(function (this: HubConnectionBuilder, url: string, options: IHttpConnectionOptions) {
      // The service's own URL and options (its transports), over the fake server.
      return withUrl.call(this, url, { ...options, httpClient: hub.http, WebSocket: hub.WebSocket });
    } as typeof HubConnectionBuilder.prototype.withUrl);
    // The service's and the client's logging.
    spyOn(console, 'log');
    spyOn(console, 'info');
    spyOn(console, 'error');

    service = TestBed.inject(SignalRService);
  });

  afterEach(async () => {
    await service.hubConnection.stop();
  });

  const connected = () => service.hubConnection.state === HubConnectionState.Connected && (hub.latest?.handshaken ?? false);

  it('negotiates, then opens the WebSocket by its connection token and completes the JSON handshake', async () => {
    await waitFor(connected, 'the connection');

    expect(hub.httpRequests.map(r => [r.method, new URL(r.url!, location.href).pathname])).toEqual([['POST', '/hub/TeamsHub/negotiate']]);
    const url = new URL(hub.latest!.url);
    expect(url.protocol).toMatch(/^wss?:$/);
    expect(url.pathname).toBe('/hub/TeamsHub');
    // The negotiate's connection token, and no access token: the session cookie goes with it (same origin)
    expect(url.searchParams.get('id')).toBe('token');
    expect(url.searchParams.has('access_token')).toBeFalse();
    expect(hub.handshakes).toContain({ protocol: 'json', version: 1 });
  });

  it('delivers the hub\'s invocations to the service\'s callbacks', async () => {
    const locked: number[] = [];
    const unlocked: number[] = [];
    const captains: unknown[] = [];
    service.lockCaptains(seasonID => locked.push(seasonID));
    service.unlockCaptains(seasonID => unlocked.push(seasonID));
    service.addCaptain(dto => captains.push(dto));
    await waitFor(connected, 'the connection');

    hub.invoke('LockCaptains', 7);
    hub.invoke('UnlockCaptains', 8);
    hub.invoke('AddCaptain', { seasonID: 7, personID: 42, teamName: "Captain's Team", captainName: 'Captain', orderNumber: 1 });
    await waitFor(() => locked.length + unlocked.length + captains.length === 3, 'the invocations');

    expect(locked).toEqual([7]);
    expect(unlocked).toEqual([8]);
    expect(captains).toEqual([{ seasonID: 7, personID: 42, teamName: "Captain's Team", captainName: 'Captain', orderNumber: 1 }]);
  });
});

describe('SignalRService where WebSockets don\'t get through', () => {
  let hub: FakeTeamsHub;
  let service: SignalRService;

  beforeEach(() => {
    hub = new FakeTeamsHub(['LongPolling']);
    const withUrl = HubConnectionBuilder.prototype.withUrl as (this: HubConnectionBuilder, url: string, options: IHttpConnectionOptions) => HubConnectionBuilder;
    spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callFake(function (this: HubConnectionBuilder, url: string, options: IHttpConnectionOptions) {
      return withUrl.call(this, url, { ...options, httpClient: hub.http, WebSocket: hub.WebSocket });
    } as typeof HubConnectionBuilder.prototype.withUrl);
    spyOn(console, 'log');
    spyOn(console, 'info');
    spyOn(console, 'error');
    service = TestBed.inject(SignalRService);
  });

  afterEach(async () => {
    await service.hubConnection.stop();
  });

  it('falls back to long polling when the server offers no WebSockets, completes the handshake and delivers over it', async () => {
    const locked: number[] = [];
    service.lockCaptains(seasonID => locked.push(seasonID));

    // The negotiate, then the connect poll by the connection's token: no WebSocket at all.
    await waitFor(() => hub.httpRequests.some(r => r.method === 'GET'), 'a poll');
    const poll = hub.httpRequests.find(r => r.method === 'GET')!;
    expect(new URL(poll.url!, location.href).searchParams.get('id')).toBe('token');
    expect(hub.sockets).toEqual([]);

    await waitFor(() => service.hubConnection.state === HubConnectionState.Connected, 'the connection');
    expect(hub.handshakes).toContain({ protocol: 'json', version: 1 });

    hub.invoke('LockCaptains', 9);
    await waitFor(() => locked.length === 1, 'the invocation, delivered over the poll');
    expect(locked).toEqual([9]);
  });
});
