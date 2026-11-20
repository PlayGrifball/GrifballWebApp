import { Injector, runInInjectionContext } from '@angular/core';
import { HubConnectionState } from '@microsoft/signalr';
import { getHubProxyFactory, getReceiverRegister } from '@signalr/TypedSignalR.Client';
import type { ILiveHubClient, ILiveHubServer } from '@signalr/TypedSignalR.Client/GrifballWebApp.Server.Live';
import { hubLifecycle } from '../signalr/hubLifecycle';

/** The site's live hub (LiveHub): what changed, and who is on the site. */
export const LIVE_HUB_URL = 'hub/LiveHub';

/** The live hub's connection, as LiveService uses it. */
export interface LiveConnection {
  /** Up now: calls go through. */
  readonly connected: boolean;
  /** Its methods (the typed client's proxy). */
  readonly hub: ILiveHubServer;
}

/**
 * Opens the live hub's connection (hubLifecycle: started, retried, made again on a sign-in or out), with `receiver` for
 * what the hub sends and `connected` called each time it comes up. Its own module, loaded by LiveService once the app
 * has started: the SignalR client is not part of the first page's download.
 */
export function openLiveConnection(injector: Injector, receiver: ILiveHubClient, connected: () => void): LiveConnection {
  const connection = runInInjectionContext(injector, () => hubLifecycle(LIVE_HUB_URL, { name: 'Live hub', connected }));
  getReceiverRegister('ILiveHubClient').register(connection, receiver);
  const hub = getHubProxyFactory('ILiveHubServer').createHubProxy(connection);
  return {
    get connected() {
      return connection.state === HubConnectionState.Connected;
    },
    hub,
  };
}
