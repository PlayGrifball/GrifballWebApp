/** What shutdown needs of the HTTP server: it stops taking connections, and calls back once the open ones end. */
export interface ClosableServer {
  close(callback?: (error?: Error) => void): unknown;
}

/** SHUTDOWN_DELAY_MS: how long the server keeps serving after SIGTERM. Default 10 s; anything not a positive number is 0. */
export function shutdownDelay(value: string | undefined): number {
  const delay = Number(value ?? 10_000);
  return Number.isFinite(delay) && delay > 0 ? delay : 0;
}

/**
 * On SIGTERM: keep serving for `delayMs` while the ingress stops sending requests (the endpoint's removal reaches
 * it after the signal), then let go of the port, send the telemetry that's queued, and exit.
 */
export function drainThenExit(server: ClosableServer, flush: () => Promise<unknown>, exit: (code: number) => void,
  delayMs: number, schedule: (run: () => void, ms: number) => unknown = setTimeout): void {
  schedule(() => {
    server.close();
    void flush().finally(() => exit(0));
  }, delayMs);
}
