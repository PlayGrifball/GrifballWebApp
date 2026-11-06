import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, PLATFORM_ID, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { AuthSession } from '../authSession.service';

/**
 * WebSockets only, no negotiate request: negotiate and the connection would be two requests that must
 * reach the same backend pod, which takes sticky sessions once there are several. A WebSocket is one
 * request, held open on whichever pod took it.
 */
export const HUB_CONNECTION_OPTIONS = { skipNegotiation: true, transport: HttpTransportType.WebSockets } as const;

/**
 * How long to wait before trying again when the connection fails to start, or closes for good once
 * automatic reconnects give up, attempt after attempt (the last one repeats; a connection resets it).
 */
export const HUB_RETRY_MS = [2_000, 5_000, 10_000, 30_000] as const;

export interface HubLifecycleOptions {
  /** What the logs call it, e.g. "Teams hub". */
  name: string;
  /**
   * Called each time the connection comes up: when it starts (also again, as another user) and when it
   * reconnects. Anything sent while it was down was missed, so whoever shows hub updates reloads then.
   */
  connected?: () => void;
}

/**
 * One hub connection's whole life, the same for every hub: built with HUB_CONNECTION_OPTIONS and automatic
 * reconnects, started at once, started again with a backoff (HUB_RETRY_MS) when a start fails or automatic
 * reconnects give up (a rollout, or a drop longer than they last) so a page left open stays live, made
 * again as the new user on a sign-in or out (the session cookie goes with the connection, and its user is
 * fixed when it connects), and stopped when the injector that made it goes. Starts and stops run one after
 * another: a start while another is still under way throws ("Cannot start a HubConnection that is not in
 * the 'Disconnected' state").
 *
 * Call it in an injection context. Only connects in the browser: on the server the connection is built
 * (with an absolute URL, as SignalR cannot resolve a relative one there) so handlers can be registered on
 * it while rendering, but never started.
 */
export function hubLifecycle(url: string, options: HubLifecycleOptions): HubConnection {
  const browser = isPlatformBrowser(inject(PLATFORM_ID));
  const connection = new HubConnectionBuilder()
    .withUrl(browser ? url : `http://localhost/${url}`, HUB_CONNECTION_OPTIONS)
    .withAutomaticReconnect()
    .build();
  if (!browser)
    return connection;

  const connected = () => options.connected?.();
  let stopped = false;
  /** Being stopped to be started again at once (a sign-in or out): its close is no reason to retry. */
  let restarting = false;
  /** A restart is queued and has not begun: another sign-in or out meanwhile needs no second one. */
  let restartQueued = false;
  let attempts = 0;
  let retry: ReturnType<typeof setTimeout> | undefined;
  let lifecycle: Promise<void> = Promise.resolve();

  const queue = (step: () => Promise<void>): void => {
    lifecycle = lifecycle.then(step);
  };
  const later = (): void => {
    clearTimeout(retry);
    retry = setTimeout(() => queue(start), HUB_RETRY_MS[Math.min(attempts++, HUB_RETRY_MS.length - 1)]);
  };
  const start = (): Promise<void> => {
    // Gone, or already connected or on its way: nothing to do
    if (stopped || connection.state !== HubConnectionState.Disconnected)
      return Promise.resolve();
    return connection.start()
      .then(() => {
        attempts = 0;
        connected();
      })
      .catch(err => {
        console.log(`Error while starting the ${options.name} connection: ${err}`);
        if (!stopped)
          later();
      });
  };

  // Back after a drop: whatever was sent meanwhile was missed
  connection.onreconnected(connected);
  // Closed: automatic reconnects gave up. A restart starts it again itself.
  connection.onclose(() => {
    if (!stopped && !restarting)
      later();
  });
  queue(start);

  const destroyRef = inject(DestroyRef);
  inject(AuthSession).changed$.pipe(takeUntilDestroyed(destroyRef)).subscribe(() => {
    if (restartQueued)
      return;
    restartQueued = true;
    // The restart connects as the new user now: no waiting out a backoff
    clearTimeout(retry);
    queue(() => {
      restartQueued = false;
      restarting = true;
      return connection.stop()
        .then(() => {
          restarting = false;
          attempts = 0;
          return start();
        })
        .catch(err => {
          restarting = false;
          console.log(`Error while restarting the ${options.name} connection: ${err}`);
        });
    });
  });

  destroyRef.onDestroy(() => {
    stopped = true;
    clearTimeout(retry);
    void connection.stop();
  });
  return connection;
}
