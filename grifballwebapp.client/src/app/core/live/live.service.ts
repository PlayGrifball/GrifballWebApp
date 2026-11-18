import { Injectable, Injector, inject, runInInjectionContext, signal } from '@angular/core';
import type { PresenceSnapshot } from '@api/models';
import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { getHubProxyFactory, getReceiverRegister } from '@signalr/TypedSignalR.Client';
import type { ILiveHubServer } from '@signalr/TypedSignalR.Client/GrifballWebApp.Server.Live';
import { hubLifecycle } from '../signalr/hubLifecycle';
import { AppLog } from '../telemetry/appLog';
import { LiveTopics } from './liveTopics';

/** The site's live hub (LiveHub): what changed, and who is on the site. */
export const LIVE_HUB_URL = 'hub/LiveHub';

/**
 * What a watcher is told: `topic` changed, with its data when it is one of the topics many watch (`data`, the server's
 * LiveSnapshots payload: DraftLiveDto, SeasonLiveDto, SeasonsLiveDto or QueueLiveDto by the topic). No topic: the
 * connection came back after a drop, and whatever changed meanwhile was missed.
 */
export interface LiveMessage {
  topic: string | null;
  data?: unknown;
}

type Listener = (message: LiveMessage) => void;

/**
 * The site's live updates, one hub connection a tab (LiveHub). A page watches the topics of what it shows
 * ({@link watch}, through liveReload) and is called when one changes, and again whenever the connection comes back
 * after a drop (what changed meanwhile was missed). The tab says which page it shows ({@link viewing}), and every tab
 * hears who is on the site ({@link presence}, the footer's). Inert until {@link connect} (provideLive, the browser's
 * app alone): a test or a page rendered on the server watches nothing and is never called.
 */
@Injectable({ providedIn: 'root' })
export class LiveService {
  private readonly log = inject(AppLog);
  private readonly injector = inject(Injector);

  private connection?: HubConnection;
  private hub?: ILiveHubServer;
  /** It came up once already: a later connection (a reconnect, a sign-in or out) may have missed changes. */
  private connectedBefore = false;

  /** Each topic's callbacks: a topic is watched on the hub while it has one. */
  private readonly listeners = new Map<string, Set<Listener>>();
  /** The viewer's own queue place: sent to them whatever they watch. */
  private readonly own = new Set<Listener>();

  private path = '/';

  private readonly _presence = signal<PresenceSnapshot | null>(null);
  /** Who is on the site, as the hub last said (null until it has). */
  readonly presence = this._presence.asReadonly();

  /** Starts the connection: once, in the browser's app (provideLive). */
  connect(): void {
    if (this.connection)
      return;
    this.connection = runInInjectionContext(this.injector, () => hubLifecycle(LIVE_HUB_URL, {
      name: 'Live hub',
      connected: () => void this.connected(),
    }));
    this.hub = getHubProxyFactory('ILiveHubServer').createHubProxy(this.connection);
    getReceiverRegister('ILiveHubClient').register(this.connection, {
      changed: topic => Promise.resolve(this.changed({ topic })),
      snapshot: (topic, data) => Promise.resolve(this.snapshot(topic, data)),
      presence: snapshot => Promise.resolve(this._presence.set(snapshot)),
    });
  }

  /**
   * Calls `listener` when one of `topics` changes (with its data, for the topics many watch), and when the connection
   * comes back after a drop. The viewer's own queue place (LiveTopics.myQueue) comes without watching. Returns the function that stops it.
   */
  watch(topics: readonly string[], listener: Listener): () => void {
    const added: string[] = [];
    for (const topic of new Set(topics)) {
      if (topic === LiveTopics.myQueue) {
        this.own.add(listener);
        continue;
      }
      let set = this.listeners.get(topic);
      if (!set) {
        set = new Set();
        this.listeners.set(topic, set);
        added.push(topic);
      }
      set.add(listener);
    }
    void this.send(hub => hub.watch(added).then(refused => this.refused(refused)), added.length > 0);
    return () => {
      this.own.delete(listener);
      const gone: string[] = [];
      for (const topic of new Set(topics)) {
        const set = this.listeners.get(topic);
        if (!set?.delete(listener) || set.size > 0)
          continue;
        this.listeners.delete(topic);
        gone.push(topic);
      }
      void this.send(hub => hub.unwatch(gone), gone.length > 0);
    };
  }

  /** The tab shows the page at `path` now (its route; the server keeps no query). */
  viewing(path: string): void {
    this.path = path.split(/[?#]/)[0] || '/';
    void this.send(hub => hub.viewing(this.path).then(snapshot => this._presence.set(snapshot)), true);
  }

  /** Watched again, and the page said again: a new connection has neither. Then, after a drop, every page reloads. */
  private async connected(): Promise<void> {
    const again = this.connectedBefore;
    this.connectedBefore = true;
    await this.send(async hub => {
      const topics = [...this.listeners.keys()];
      if (topics.length > 0)
        this.refused(await hub.watch(topics));
      this._presence.set(await hub.viewing(this.path));
    }, true);
    if (again)
      this.notify([...new Set([...this.own, ...[...this.listeners.values()].flatMap(set => [...set])])], { topic: null });
  }

  private changed(message: LiveMessage): void {
    this.notify(message.topic === LiveTopics.myQueue ? [...this.own] : [...this.listeners.get(message.topic ?? '') ?? []], message);
  }

  /** A topic's data, parsed once for all its watchers; data that won't parse is a change without it (the pages read again). */
  private snapshot(topic: string, data: string): void {
    let parsed: unknown;
    try {
      parsed = JSON.parse(data);
    } catch (error) {
      this.log.warn(`Live hub: 's data did not parse`, error);
    }
    this.changed(parsed === undefined ? { topic } : { topic, data: parsed });
  }

  /** Each listener on its own: one that throws doesn't keep the change from the rest. */
  private notify(listeners: Listener[], message: LiveMessage): void {
    for (const listener of listeners) {
      try {
        listener(message);
      } catch (error) {
        this.log.error('Live hub: a page failed to take a change', error);
      }
    }
  }

  private refused(topics: string[]): void {
    if (topics.length > 0)
      this.log.debug('Live hub: not watching', topics);
  }

  /** A call on the hub while it is connected (it is made again on connecting); a failure is logged, not thrown. */
  private async send(call: (hub: ILiveHubServer) => Promise<unknown>, needed: boolean): Promise<void> {
    if (!needed || !this.hub || this.connection?.state !== HubConnectionState.Connected)
      return;
    try {
      await call(this.hub);
    } catch (error) {
      this.log.warn('Live hub: a call failed', error);
    }
  }
}
