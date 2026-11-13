/** The prefixes the server passes on to the backend (src/server.ts). */
export const BACKEND_PREFIXES = ['/api', '/hub', '/signin-discord', '/signalr-dev'] as const;
export type BackendPrefix = typeof BACKEND_PREFIXES[number];

/** The hubs' page (TypedSignalR.Client.DevTools, as /api/swagger is the API's): it lives at /signalr-dev on the backend. */
export const SIGNALR_PAGE = '/signalr-dev';

/**
 * A request's path on the backend, as nginx proxied it: /api/x and /hub/x as /x; /signin-discord (the Discord sign-in
 * callback) and the hubs' page as they are, but for the page's own hub connections (/signalr-dev/TeamsHub, a name
 * ending in Hub: the page connects relative to itself), which go to the hub (/TeamsHub).
 */
export function backendPath(prefix: BackendPrefix, path: string): string {
  if (prefix === '/signin-discord')
    return path;
  if (prefix === SIGNALR_PAGE)
    return /^\/signalr-dev\/\w*Hub(?=[/?]|$)/.test(path) ? path.slice(prefix.length) : path;
  return path.slice(prefix.length) || '/';
}

/** Whether a prefix's requests may upgrade to a WebSocket (SignalR's): the hubs, and the hubs' page's connections. */
export const isWebSocketPrefix = (prefix: BackendPrefix): boolean => prefix === '/hub' || prefix === SIGNALR_PAGE;
