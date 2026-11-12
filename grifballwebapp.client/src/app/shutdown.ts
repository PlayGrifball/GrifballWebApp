/** What shutdown needs of the HTTP server: it stops taking connections, and calls back once the open ones end. */
export interface ClosableServer {
  close(callback?: (error?: Error) => void): unknown;
}

/** SHUTDOWN_DELAY_MS: how long the server keeps serving after SIGTERM. Default 10 s; anything not a positive number is 0. */
export function shutdownDelay(value: string | undefined): number {
  const delay = Number(value ?? 10_000);
  return Number.isFinite(delay) && delay > 0 ? delay : 0;
}

/** How long, once the port is closed, the requests still open get to finish (a SignalR WebSocket never does). */
export const CLOSE_CAP_MS = 15_000;

/** How long sending the queued telemetry may take (a collector that's slow or away). */
export const FLUSH_CAP_MS = 3_000;

/**
 * On SIGTERM: keep serving for `delayMs` while the ingress stops sending requests (the endpoint's removal reaches
 * it after the signal), then let go of the port and wait for the requests still open to finish, at most
 * CLOSE_CAP_MS; then send the telemetry that's queued, for at most FLUSH_CAP_MS, and exit. 10 + 15 + 3 s stays inside
 * Kubernetes' 30 s grace period.
 */
export function drainThenExit(server: ClosableServer, flush: () => Promise<unknown>, exit: (code: number) => void,
  delayMs: number, schedule: (run: () => void, ms: number) => unknown = setTimeout, capMs = CLOSE_CAP_MS): void {
  let closed = false;
  let exited = false;
  const exitOnce = () => {
    if (!exited) {
      exited = true;
      exit(0);
    }
  };
  const finish = () => {
    if (closed)
      return;
    closed = true;
    schedule(exitOnce, FLUSH_CAP_MS);
    void flush().finally(exitOnce);
  };
  schedule(() => {
    server.close(() => finish());
    schedule(finish, capMs);
  }, delayMs);
}
