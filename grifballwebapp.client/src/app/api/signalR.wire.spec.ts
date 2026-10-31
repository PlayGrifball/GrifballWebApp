import { TestBed } from '@angular/core/testing';
import {
  HttpClient, HttpRequest, HttpResponse, HubConnectionBuilder, HubConnectionState, IHttpConnectionOptions,
} from '@microsoft/signalr';
import { SignalRService } from './signalR.service';

/** The record separator ending every JSON hub protocol message. */
const RS = '\x1e';

/**
 * What the server side of ASP.NET Core SignalR says to the client over a WebSocket: the JSON hub protocol (handshake,
 * invocations). It runs the real @microsoft/signalr client through SignalRService, with the service's own options, so a
 * client upgrade that changes the wire protocol or how the options are used fails here. The service skips negotiate
 * (no sticky sessions needed with several backend pods), so any HTTP request is recorded, and must not happen. The
 * other spec (signalR.service.spec.ts) replaces the connection with a stub.
 */
class FakeTeamsHub {
  readonly httpRequests: HttpRequest[] = [];
  readonly sockets: FakeSocket[] = [];
  readonly handshakes: unknown[] = [];
  readonly fromClient: { type: number }[] = [];

  /** Every HTTP request the client makes (negotiate would be one): recorded and refused. */
  readonly http = new (class extends HttpClient {
    constructor(private readonly hub: FakeTeamsHub) { super(); }
    override send(request: HttpRequest): Promise<HttpResponse> {
      this.hub.httpRequests.push(request);
      return Promise.resolve(new HttpResponse(404, 'Not Found', ''));
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

  /** Sends a hub invocation (the JSON hub protocol's type 1) down the newest socket. */
  invoke(target: string, ...args: unknown[]): void {
    this.latest!.deliver(JSON.stringify({ type: 1, target, arguments: args }) + RS);
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
      // The service's own URL and options (transport, skipNegotiation), over the fake server.
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

  it('opens the WebSocket straight away, with no negotiate request, and completes the JSON handshake', async () => {
    await waitFor(connected, 'the connection');

    expect(hub.httpRequests).toEqual([]);
    const url = new URL(hub.latest!.url);
    expect(url.protocol).toMatch(/^wss?:$/);
    expect(url.pathname).toBe('/hub/TeamsHub');
    // No connection token from a negotiate, and no access token: the session cookie goes with it (same origin)
    expect(url.searchParams.has('id')).toBeFalse();
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
