import { TestBed } from '@angular/core/testing';
import {
  HttpClient, HttpRequest, HttpResponse, HubConnectionBuilder, IHttpConnectionOptions,
} from '@microsoft/signalr';
import { LiveService } from './live.service';
import { LiveTopics } from './liveTopics';

/** The record separator ending every JSON hub protocol message. */
const RS = '\x1e';

/**
 * What the server side of ASP.NET Core SignalR says to the client over a WebSocket or long polling: the JSON hub
 * protocol (handshake, invocations), and for long polling the HTTP mechanics around it (a connect poll, then a
 * background one held until there is data, and the handshake and invocations the client sends as POSTs). It runs the
 * real @microsoft/signalr client through LiveService, with the service's own options, so a client upgrade that
 * changes the wire protocol or how the options are used fails here. It answers the client's invocations as LiveHub
 * does: Watch with none refused, Viewing with who is on the site.
 */
class FakeLiveHub {
  readonly httpRequests: HttpRequest[] = [];
  readonly sockets: FakeSocket[] = [];
  readonly handshakes: unknown[] = [];
  readonly fromClient: { type: number }[] = [];

  /** The completion LiveHub sends for an invocation the client waits on (with an ID): Watch refuses none, Viewing says who is on. */
  completion(message: { type: number, target?: string, invocationId?: string }): string | null {
    if (message.type !== 1 || message.invocationId === undefined)
      return null;
    const results: Record<string, unknown> = { Watch: [], Viewing: SNAPSHOT };
    const result = message.target === undefined ? undefined : results[message.target];
    return JSON.stringify({ type: 3, invocationId: message.invocationId, result }) + RS;
  }

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
    constructor(private readonly hub: FakeLiveHub) { super(); }
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
            const message = JSON.parse(frame);
            this.hub.fromClient.push(message);
            const completion = this.hub.completion(message);
            if (completion)
              this.deliver(completion);
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

    /** Resolves the open background poll with data, as the server would; queued (after any already queued) if none is open yet. */
    deliver(data: string): void {
      if (this.pendingPoll) {
        const resolve = this.pendingPoll;
        this.pendingPoll = null;
        resolve(new HttpResponse(200, 'OK', data));
      } else {
        this.queued = (this.queued ?? '') + data;
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

  constructor(private readonly hub: FakeLiveHub, readonly url: string) {
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
        const message = JSON.parse(frame);
        this.hub.fromClient.push(message);
        const completion = this.hub.completion(message);
        if (completion)
          this.deliver(completion);
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

/** Who is on the site, as the hub answers Viewing and sends it. */
const SNAPSHOT = { online: 1, members: 0, guests: 1, pages: [{ path: '/seasons', guests: 1, members: [] }] };

/**
 * A fake hub for each test of the describe it is called in (offering these transports), and the service connected to
 * it with its own URL and options. The test's injector going stops the connection (hubLifecycle).
 */
function useFakeHub(offered?: ConstructorParameters<typeof FakeLiveHub>[0]): { hub: FakeLiveHub; service: LiveService } {
  const current = {} as { hub: FakeLiveHub; service: LiveService };
  beforeEach(() => {
    const hub = new FakeLiveHub(offered);
    const withUrl = HubConnectionBuilder.prototype.withUrl as (this: HubConnectionBuilder, url: string, options: IHttpConnectionOptions) => HubConnectionBuilder;
    spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callFake(function (this: HubConnectionBuilder, url: string, options: IHttpConnectionOptions) {
      // The service's own URL and options (its transports), over the fake server.
      return withUrl.call(this, url, { ...options, httpClient: hub.http, WebSocket: hub.WebSocket });
    } as typeof HubConnectionBuilder.prototype.withUrl);
    // The service's and the client's logging.
    spyOn(console, 'log');
    spyOn(console, 'info');
    spyOn(console, 'warn');
    spyOn(console, 'error');

    current.hub = hub;
    current.service = TestBed.inject(LiveService);
  });
  return current;
}

/** The hub methods the client called, with their arguments. */
const called = (hub: FakeLiveHub) => hub.fromClient
  .filter((m): m is { type: number, target: string, arguments: unknown[] } => m.type === 1)
  .map(m => [m.target, ...m.arguments]);

describe('LiveService over the SignalR wire protocol', () => {
  const fake = useFakeHub();
  let hub: FakeLiveHub;
  let service: LiveService;
  beforeEach(() => {
    hub = fake.hub;
    service = fake.service;
  });

  it('negotiates, then opens the WebSocket by its connection token and completes the JSON handshake', async () => {
    void service.connect();
    await waitFor(() => hub.latest?.handshaken ?? false, 'the connection');

    expect(hub.httpRequests.map(r => [r.method, new URL(r.url!, location.href).pathname])).toEqual([['POST', '/hub/LiveHub/negotiate']]);
    const url = new URL(hub.latest!.url);
    expect(url.protocol).toMatch(/^wss?:$/);
    expect(url.pathname).toBe('/hub/LiveHub');
    // The negotiate's connection token, and no access token: the session cookie goes with it (same origin)
    expect(url.searchParams.get('id')).toBe('token');
    expect(url.searchParams.has('access_token')).toBeFalse();
    expect(hub.handshakes).toContain({ protocol: 'json', version: 1 });
  });

  it('watches the topics watched before it connected, says the page, and takes who is on the site', async () => {
    service.watch(['season:5', 'queue:me'], () => undefined);
    void service.connect();

    await waitFor(() => service.presence() !== null, 'the page said');

    expect(called(hub)).toEqual([['Watch', ['season:5']], ['Viewing', '/']]);
    expect(service.presence()).toEqual(SNAPSHOT);
  });

  it('calls the watchers of the topic that changed alone, and the viewer\'s own queue place without watching it', async () => {
    const changed: string[] = [];
    service.watch(['season:5'], () => changed.push('season'));
    service.watch(['queue'], () => changed.push('queue'));
    service.watch([LiveTopics.myQueue], () => changed.push('mine'));
    void service.connect();
    await waitFor(() => service.presence() !== null, 'the connection');

    hub.invoke('Changed', 'season:5');
    hub.invoke('Changed', 'queue:me');
    await waitFor(() => changed.length === 2, 'the changes');

    expect(changed).toEqual(['season', 'mine']);
  });

  it('hands a topic\'s data, parsed once, to its watchers', async () => {
    const got: unknown[] = [];
    service.watch(['queue'], message => got.push(message));
    service.watch(['queue'], message => got.push(message));
    void service.connect();
    await waitFor(() => service.presence() !== null, 'the connection');

    hub.invoke('Snapshot', 'queue', '{"count":2}');
    await waitFor(() => got.length === 2, 'the data');

    expect(got).toEqual([{ topic: 'queue', data: { count: 2 } }, { topic: 'queue', data: { count: 2 } }]);
    expect((got[0] as { data: unknown }).data).toBe((got[1] as { data: unknown }).data);
  });

  it('stops watching a topic its last watcher left', async () => {
    void service.connect();
    await waitFor(() => service.presence() !== null, 'the connection');

    const first = service.watch(['season:5'], () => undefined);
    const second = service.watch(['season:5'], () => undefined);
    first();
    second();
    await waitFor(() => called(hub).some(([target]) => target === 'Unwatch'), 'the unwatch');

    expect(called(hub).filter(([target]) => target === 'Watch' || target === 'Unwatch')).toEqual([['Watch', ['season:5']], ['Unwatch', ['season:5']]]);
  });

  it('takes who is on the site whenever the hub sends it', async () => {
    void service.connect();
    await waitFor(() => service.presence() !== null, 'the connection');

    hub.invoke('Presence', { ...SNAPSHOT, online: 3 });
    await waitFor(() => service.presence()?.online === 3, 'the snapshot');

    expect(service.presence()?.online).toBe(3);
  });
});

describe('LiveService where WebSockets don\'t get through', () => {
  const fake = useFakeHub(['LongPolling']);

  it('falls back to long polling when the server offers no WebSockets, completes the handshake and delivers over it', async () => {
    const { hub, service } = fake;
    const changed: string[] = [];
    service.watch(['seasons'], () => changed.push('seasons'));
    void service.connect();

    // The negotiate, then the connect poll by the connection's token: no WebSocket at all.
    await waitFor(() => hub.httpRequests.some(r => r.method === 'GET'), 'a poll');
    const poll = hub.httpRequests.find(r => r.method === 'GET')!;
    expect(new URL(poll.url!, location.href).searchParams.get('id')).toBe('token');
    expect(hub.sockets).toEqual([]);

    await waitFor(() => service.presence() !== null, 'the connection');
    expect(hub.handshakes).toContain({ protocol: 'json', version: 1 });

    hub.invoke('Changed', 'seasons');
    await waitFor(() => changed.length === 1, 'the invocation, delivered over the poll');
    expect(changed).toEqual(['seasons']);
  });
});
