import { TestBed } from '@angular/core/testing';
import {
  HttpClient, HttpRequest, HttpResponse, HttpTransportType, HubConnectionBuilder, HubConnectionState, IHttpConnectionOptions,
} from '@microsoft/signalr';
import { SignalRService } from './signalR.service';

/** The record separator ending every JSON hub protocol message. */
const RS = '\x1e';

interface FakeConnection {
  queue: string[];
  polled: boolean;
  handshaken: boolean;
  closed: boolean;
  waiting?: (response: HttpResponse) => void;
}

/**
 * What the server side of ASP.NET Core SignalR says to the client over HTTP: negotiate (version 1), then long polling
 * with the JSON hub protocol (handshake, invocations). It runs the real @microsoft/signalr client through SignalRService,
 * so a client upgrade that changes the wire protocol or how the service's options are used fails here. The other spec
 * (signalR.service.spec.ts) replaces the connection with a stub, and the e2e tests answer the hub with a 404.
 */
class FakeTeamsHub extends HttpClient {
  readonly negotiations: HttpRequest[] = [];
  readonly handshakes: unknown[] = [];
  readonly fromClient: { type: number }[] = [];
  private readonly connections = new Map<string, FakeConnection>();
  private latest?: FakeConnection;
  private count = 0;

  override send(request: HttpRequest): Promise<HttpResponse> {
    const url = new URL(request.url!, window.location.href);
    if (request.method === 'POST' && url.pathname.endsWith('/hub/TeamsHub/negotiate')) {
      this.negotiations.push(request);
      const token = `connection-token-${++this.count}`;
      this.latest = { queue: [], polled: false, handshaken: false, closed: false };
      this.connections.set(token, this.latest);
      return Promise.resolve(new HttpResponse(200, 'OK', JSON.stringify({
        negotiateVersion: 1,
        connectionId: `connection-id-${this.count}`,
        connectionToken: token,
        availableTransports: [{ transport: 'LongPolling', transferFormats: ['Text', 'Binary'] }],
      })));
    }

    const connection = url.pathname.endsWith('/hub/TeamsHub') ? this.connections.get(url.searchParams.get('id') ?? '') : undefined;
    if (!connection) {
      return Promise.resolve(new HttpResponse(404, 'Not Found', ''));
    }
    switch (request.method) {
      case 'GET':
        return this.poll(connection, request);
      case 'POST':
        this.receive(connection, request.content as string);
        return Promise.resolve(new HttpResponse(200, 'OK', ''));
      case 'DELETE':
        connection.closed = true;
        this.flush(connection);
        return Promise.resolve(new HttpResponse(202, 'Accepted', ''));
      default:
        return Promise.resolve(new HttpResponse(405, 'Method Not Allowed', ''));
    }
  }

  /** Sends a hub invocation (the JSON hub protocol's type 1) to the newest connection. */
  invoke(target: string, ...args: unknown[]): void {
    this.latest!.queue.push(JSON.stringify({ type: 1, target, arguments: args }) + RS);
    this.flush(this.latest!);
  }

  get latestHandshaken(): boolean {
    return this.latest?.handshaken ?? false;
  }

  private poll(connection: FakeConnection, request: HttpRequest): Promise<HttpResponse> {
    if (!connection.polled) {
      // The server answers the transport's first poll at once: it only confirms the connection.
      connection.polled = true;
      return Promise.resolve(new HttpResponse(200, 'OK', ''));
    }
    return new Promise(resolve => {
      connection.waiting = resolve;
      if (request.abortSignal) {
        request.abortSignal.onabort = () => resolve(new HttpResponse(204, 'No Content', ''));
      }
      this.flush(connection);
    });
  }

  private receive(connection: FakeConnection, content: string): void {
    for (const frame of content.split(RS).filter(f => f.length > 0)) {
      if (!connection.handshaken) {
        this.handshakes.push(JSON.parse(frame));
        connection.handshaken = true;
        connection.queue.push('{}' + RS);
      } else {
        this.fromClient.push(JSON.parse(frame));
      }
    }
    this.flush(connection);
  }

  private flush(connection: FakeConnection): void {
    const resolve = connection.waiting;
    if (!resolve || (!connection.closed && connection.queue.length === 0)) {
      return;
    }
    connection.waiting = undefined;
    if (connection.closed) {
      resolve(new HttpResponse(204, 'No Content', ''));
    } else {
      resolve(new HttpResponse(200, 'OK', connection.queue.splice(0).join('')));
    }
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
      // The service's own URL and options, over the fake server and the one transport it offers.
      return withUrl.call(this, url, { ...options, httpClient: hub, transport: HttpTransportType.LongPolling });
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

  it('negotiates with the session cookie alone (no token), completes the JSON handshake and connects', async () => {
    await waitFor(() => service.hubConnection.state === HubConnectionState.Connected && hub.latestHandshaken, 'the connection');

    expect(hub.negotiations.length).toBeGreaterThan(0);
    for (const negotiation of hub.negotiations) {
      expect(negotiation.headers?.['Authorization']).toBeUndefined();
      // Cookies go along (same origin)
      expect(negotiation.withCredentials).not.toBe(false);
    }
    expect(hub.handshakes).toContain({ protocol: 'json', version: 1 });
  });

  it('delivers the hub\'s invocations to the service\'s callbacks', async () => {
    const locked: number[] = [];
    const unlocked: number[] = [];
    const captains: unknown[] = [];
    service.lockCaptains(seasonID => locked.push(seasonID));
    service.unlockCaptains(seasonID => unlocked.push(seasonID));
    service.addCaptain(dto => captains.push(dto));
    await waitFor(() => service.hubConnection.state === HubConnectionState.Connected && hub.latestHandshaken, 'the connection');

    hub.invoke('LockCaptains', 7);
    hub.invoke('UnlockCaptains', 8);
    hub.invoke('AddCaptain', { seasonID: 7, personID: 42, teamName: "Captain's Team", captainName: 'Captain', orderNumber: 1 });
    await waitFor(() => locked.length + unlocked.length + captains.length === 3, 'the invocations');

    expect(locked).toEqual([7]);
    expect(unlocked).toEqual([8]);
    expect(captains).toEqual([{ seasonID: 7, personID: 42, teamName: "Captain's Team", captainName: 'Captain', orderNumber: 1 }]);
  });
});
