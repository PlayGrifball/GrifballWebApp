// A minimal SignalR server for the hubs the pages use (/hub/LiveHub and /hub/ChatHub, see HUBS), for
// e2e/serve.mjs: the production server (src/server.ts) proxies /hub to the backend (including the
// WebSocket upgrade), and without an answer there every page (the live hub is the app's, one a tab)
// logs a failed connection to the console on every load, which fails Lighthouse's best-practices
// errors-in-console audit.
//
// The pages' own changes are plain API calls; on the live hub the tab only says what it watches and
// which page it shows (LiveService: Watch, Unwatch and Viewing), and waits for those answers, so
// liveHubReplies answers them (no topic refused; a site of one guest). The client negotiates first
// (core/signalr/hubLifecycle.ts), so this answers the negotiate offering WebSockets, then accepts the
// WebSocket, completes the JSON Hub Protocol handshake and keeps the connection alive with pings.
// e2e/zoneless.spec.ts's live test mocks the same hub at the Playwright page.route/routeWebSocket
// level (for a test that pushes a change down it); the two can't share the transport (this one
// speaks raw HTTP upgrade + WebSocket frames for a real socket, that one intercepts in the browser),
// but they share the record separator, the negotiate and the invocation answers below.
import { createHash, randomUUID } from 'node:crypto';
import type { IncomingMessage } from 'node:http';
import type { Socket } from 'node:net';

/** The JSON Hub Protocol's record separator, after every message (handshake, ping, invocation). */
export const RS = '\u001e';

const WEBSOCKET_GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';
// SignalR's default server timeout is 30s without a message; ping well inside that.
const PING_INTERVAL_MS = 10_000;

/** The hubs answered here: the live hub (every page's live updates, LiveService) and the chat hub. */
const HUBS = new Set(['/LiveHub', '/ChatHub']);

/** Who is on the site, as Viewing answers it: one guest, on the home page. */
export const PRESENCE = { online: 1, members: 0, guests: 1, pages: [{ path: '/', guests: 1, members: [] }] };

/**
 * The answers to what the client sent (`text`: one or more JSON Hub Protocol messages, each ending in RS): a
 * completion for each live hub invocation the client waits on (Watch: no topic refused; Unwatch; Viewing: PRESENCE),
 * each ending in RS. Anything else (pings, the chat hub's calls) gets none.
 */
export function liveHubReplies(text: string): string[] {
  const replies: string[] = [];
  for (const raw of text.split(RS)) {
    if (!raw.trim())
      continue;
    let message: { type?: number, invocationId?: string, target?: string };
    try {
      message = JSON.parse(raw);
    } catch {
      continue;
    }
    if (message.type !== 1 || message.invocationId === undefined)
      continue;
    const id = message.invocationId;
    if (message.target === 'Watch')
      replies.push(JSON.stringify({ type: 3, invocationId: id, result: [] }) + RS);
    else if (message.target === 'Unwatch')
      replies.push(JSON.stringify({ type: 3, invocationId: id }) + RS);
    else if (message.target === 'Viewing')
      replies.push(JSON.stringify({ type: 3, invocationId: id, result: PRESENCE }) + RS);
  }
  return replies;
}

/** Whether `req` is one of HUBS' WebSocket upgrade, as the mock backend sees it (/hub stripped by the
 * proxy, see src/server.ts's toBackend). */
export function isHubUpgrade(req: IncomingMessage): boolean {
  return HUBS.has((req.url ?? '').split('?')[0]);
}

/** Whether `req` is one of HUBS' negotiate requests (POST /LiveHub/negotiate?negotiateVersion=1), as the mock backend sees it. */
export function isHubNegotiate(req: IncomingMessage): boolean {
  const path = (req.url ?? '').split('?')[0];
  return req.method === 'POST' && path.endsWith('/negotiate') && HUBS.has(path.slice(0, -'/negotiate'.length));
}

/** A negotiate's answer (version 1): a fresh connection, WebSockets offered, as the real hub offers them first. */
export function negotiateResponse(): string {
  const id = randomUUID();
  return JSON.stringify({
    negotiateVersion: 1, connectionId: id, connectionToken: id,
    availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }],
  });
}

/**
 * Completes the WebSocket handshake for `req`/`socket` and runs the JSON Hub Protocol on it: accepts
 * the client's handshake ({}), answers pings and pings back, and answers the live hub's invocations
 * (liveHubReplies); every other message is ignored.
 */
export function handleHubUpgrade(req: IncomingMessage, socket: Socket): void {
  const key = req.headers['sec-websocket-key'];
  if (typeof key !== 'string') {
    socket.destroy();
    return;
  }
  // SHA-1 because the WebSocket handshake (RFC 6455) is defined with it: no secret, no other choice
  // eslint-disable-next-line sonarjs/hashing -- SHA-1 is the WebSocket handshake's own hash (RFC 6455), no secret
  const accept = createHash('sha1').update(key + WEBSOCKET_GUID).digest('base64'); // NOSONAR(S4790): the protocol's own hash
  socket.write([
    'HTTP/1.1 101 Switching Protocols',
    'Upgrade: websocket',
    'Connection: Upgrade',
    `Sec-WebSocket-Accept: ${accept}`,
    '', '',
  ].join('\r\n'));

  let handshook = false;
  let buffer = Buffer.alloc(0);
  const pingTimer = setInterval(() => {
    if (handshook)
      sendText(socket, `{"type":6}${RS}`);
  }, PING_INTERVAL_MS);
  pingTimer.unref();
  const stop = () => clearInterval(pingTimer);
  socket.on('error', stop);
  socket.on('close', stop);

  socket.on('data', (chunk: Buffer) => {
    buffer = Buffer.concat([buffer, chunk]);
    let frame;
    while ((frame = readClientFrame(buffer)) != null) {
      buffer = buffer.subarray(frame.consumed);
      if (frame.opcode === 0x8) { // close
        socket.end();
        return;
      }
      if (frame.opcode === 0x9) { // ping
        sendFrame(socket, 0xA, frame.payload); // pong
        continue;
      }
      if (frame.opcode !== 0x1)
        continue;
      let text = frame.payload.toString('utf8');
      if (!handshook) {
        // The JSON Hub Protocol's own handshake request ({"protocol":"json","version":1}<RS>): an
        // empty object accepts it, same as the real hub. Messages may follow it in the same frame.
        handshook = true;
        sendText(socket, `{}${RS}`);
        text = text.slice(text.indexOf(RS) + 1);
      }
      for (const reply of liveHubReplies(text))
        sendText(socket, reply);
    }
  });
}

function sendText(socket: Socket, text: string): void {
  sendFrame(socket, 0x1, Buffer.from(text, 'utf8'));
}

function sendFrame(socket: Socket, opcode: number, payload: Buffer): void {
  const len = payload.length;
  const header = len < 126
    ? Buffer.from([0x80 | opcode, len])
    : Buffer.concat([Buffer.from([0x80 | opcode, 126]), uint16be(len)]);
  if (!socket.destroyed)
    socket.write(Buffer.concat([header, payload]));
}

function uint16be(n: number): Buffer {
  const buf = Buffer.alloc(2);
  buf.writeUInt16BE(n);
  return buf;
}

/** One masked client frame off the front of `buffer` (RFC 6455); null if it is not complete yet. */
function readClientFrame(buffer: Buffer): { opcode: number, payload: Buffer, consumed: number } | null {
  if (buffer.length < 2)
    return null;
  const opcode = buffer[0] & 0x0f;
  const masked = (buffer[1] & 0x80) !== 0;
  let len = buffer[1] & 0x7f;
  let offset = 2;
  if (len === 126) {
    if (buffer.length < 4)
      return null;
    len = buffer.readUInt16BE(2);
    offset = 4;
  } else if (len === 127) {
    if (buffer.length < 10)
      return null;
    len = Number(buffer.readBigUInt64BE(2));
    offset = 10;
  }
  const maskLength = masked ? 4 : 0;
  if (buffer.length < offset + maskLength + len)
    return null;
  let payload = buffer.subarray(offset + maskLength, offset + maskLength + len);
  if (masked) {
    const mask = buffer.subarray(offset, offset + 4);
    const unmasked = Buffer.alloc(len);
    for (let i = 0; i < len; i++)
      unmasked[i] = payload[i] ^ mask[i % 4];
    payload = unmasked;
  }
  return { opcode, payload, consumed: offset + maskLength + len };
}
