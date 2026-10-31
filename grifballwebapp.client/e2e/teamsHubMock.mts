// A minimal SignalR server for the Teams hub (/hub/TeamsHub), for e2e/serve.mjs: the production
// server (src/server.ts) proxies /hub to the backend (including the WebSocket upgrade), and without
// an answer there the team builder page (/season/:id/teams) logs "Failed to complete negotiation"
// to the console on every load, which fails Lighthouse's best-practices errors-in-console audit.
//
// The page only *listens* on the hub (SignalRService.addCaptain and friends): its own changes are
// plain API calls, not hub invocations (teamBuilder.component.ts never calls hubConnection.invoke
// or .send). So this only has to negotiate, complete the JSON Hub Protocol handshake over the
// WebSocket and keep the connection alive with pings; nothing a Lighthouse run does needs a real
// invocation answered. e2e/zoneless.spec.ts's SignalR test mocks the same hub at the Playwright
// page.route/routeWebSocket level (for a test that pushes a change down it); the two can't share
// code (this one speaks raw HTTP upgrade + WebSocket frames for a real socket, that one intercepts
// in the browser), but they share the wire shapes below so the two fakes agree with each other and
// with the real hub.
import { createHash, randomUUID } from 'node:crypto';
import type { IncomingMessage } from 'node:http';
import type { Socket } from 'node:net';

/** The JSON Hub Protocol's record separator, after every message (handshake, ping, invocation). */
export const RS = '\u001e';

/** negotiate's response body (POST /hub/TeamsHub/negotiate): WebSockets only, the one transport the
 * client and e2e/zoneless.spec.ts's fake both try. */
export function teamsHubNegotiateBody(): object {
  return {
    negotiateVersion: 1,
    connectionId: randomUUID(),
    connectionToken: randomUUID(),
    availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }],
  };
}

const WEBSOCKET_GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';
// SignalR's default server timeout is 30s without a message; ping well inside that.
const PING_INTERVAL_MS = 10_000;

/** Whether `req` is the hub's WebSocket upgrade, as the mock backend sees it (/hub stripped by the
 * proxy, see src/server.ts's toBackend). */
export function isTeamsHubUpgrade(req: IncomingMessage): boolean {
  return (req.url ?? '').split('?')[0] === '/TeamsHub';
}

/**
 * Completes the WebSocket handshake for `req`/`socket` and runs the JSON Hub Protocol on it: accepts
 * the client's handshake ({}), answers pings and pings back, and otherwise ignores every message (no
 * invocation the team builder sends needs an answer; see the module comment).
 */
export function handleTeamsHubUpgrade(req: IncomingMessage, socket: Socket): void {
  const key = req.headers['sec-websocket-key'];
  if (typeof key !== 'string') {
    socket.destroy();
    return;
  }
  const accept = createHash('sha1').update(key + WEBSOCKET_GUID).digest('base64');
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

  socket.on('data', chunk => {
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
      if (frame.opcode === 0x1 && !handshook) {
        // The JSON Hub Protocol's own handshake request ({"protocol":"json","version":1}<RS>): an
        // empty object accepts it, same as the real hub.
        handshook = true;
        sendText(socket, `{}${RS}`);
      }
      // Any later message is an invocation; nothing the page sends needs one answered (module comment).
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
