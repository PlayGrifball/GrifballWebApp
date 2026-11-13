import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, PLATFORM_ID, inject } from '@angular/core';

import { BROWSER_TELEMETRY_MARK, CLIENT_TELEMETRY_PATH } from './telemetryShared';

export { REQUEST_KIND_ATTRIBUTE } from './telemetryShared';

// The browser's telemetry: its spans (a navigation, a call to the API), its log lines and its Web Vitals, sent in
// batches to the API (POST /api/client-telemetry, GrifballWebApp.Server/Telemetry/Browser), which passes them on to
// the collector as this app's. No OpenTelemetry SDK: the few things it needs are here. Nothing is sent on the server,
// from an automated browser (tests, Lighthouse), or once the API refuses a batch (404: telemetry is off there).

export const TELEMETRY_URL = CLIENT_TELEMETRY_PATH;
export const FLUSH_INTERVAL_MS = 5_000;
/** What one batch carries of each (the API's limits). */
export const MAX_BATCH = 100;
/** What waits to be sent, at most, of each: the oldest go first. */
export const MAX_QUEUE = 500;
/** fetch's keepalive (which outlives the page) takes at most 64 KiB in flight, in bytes. */
export const MAX_KEEPALIVE_BYTES = 60_000;
/** The API's limits (ClientTelemetryDtos): a batch past them is refused whole. */
export const MAX_SPAN_NAME = 200;
export const MAX_LOG_MESSAGE = 4_000;
export const MAX_VITAL = 600_000;

export type SpanKind = 'internal' | 'client';
export type SpanStatus = 'unset' | 'ok' | 'error';
export type BrowserLogLevel = 'debug' | 'info' | 'warn' | 'error';
export type AttributeValue = string | number | boolean;
export type Attributes = Record<string, AttributeValue | undefined>;

/** A span's place in its trace. */
export interface TraceContext {
  traceId: string;
  spanId: string;
}

/** What the API takes (ClientSpanDto): times in epoch milliseconds. */
export interface SpanData extends TraceContext {
  parentSpanId?: string;
  name: string;
  kind: SpanKind;
  start: number;
  end: number;
  status: SpanStatus;
  attributes: Record<string, AttributeValue>;
}

/** ClientLogDto. */
export interface LogData {
  time: number;
  level: BrowserLogLevel;
  message: string;
  traceId?: string;
  spanId?: string;
  attributes: Record<string, AttributeValue>;
}

/** ClientVitalDto: web-vitals' name and value. */
export interface VitalData {
  name: 'LCP' | 'INP' | 'CLS' | 'FCP' | 'TTFB';
  value: number;
}

export interface TelemetryBatch {
  spans: SpanData[];
  logs: LogData[];
  vitals: VitalData[];
}

/** `bytes` random bytes as hex: a trace id (16) or a span id (8), never all zeros (W3C calls that invalid). */
export function randomId(bytes: number): string {
  const values = new Uint8Array(bytes);
  do
    crypto.getRandomValues(values);
  while (values.every(b => b === 0));
  return [...values].map(b => b.toString(16).padStart(2, '0')).join('');
}

const TRACEPARENT = /^00-([0-9a-f]{32})-([0-9a-f]{16})-[0-9a-f]{2}$/;

/** A W3C traceparent's trace and span ids; undefined for anything else. */
export function parseTraceparent(value: string | null | undefined): TraceContext | undefined {
  const match = value ? TRACEPARENT.exec(value) : null;
  return match ? { traceId: match[1], spanId: match[2] } : undefined;
}

export function formatTraceparent(context: TraceContext): string {
  return `00-${context.traceId}-${context.spanId}-01`;
}

/**
 * Whether the page says browser telemetry is on: the app's server marks its Server-Timing (BROWSER_TELEMETRY) when the
 * API takes it. Without the mark nothing is recorded, so no call carries a traceparent whose span never arrives.
 */
export function browserTelemetryOn(perf: Pick<Performance, 'getEntriesByType'> | undefined = globalThis.performance): boolean {
  const navigation = perf?.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
  return navigation?.serverTiming?.some(t => t.name === BROWSER_TELEMETRY_MARK) ?? false;
}

/** The time now in epoch milliseconds, to the microsecond where the browser allows. */
export function epochMs(): number {
  return performance.timeOrigin + performance.now();
}

/** Attributes without the ones left undefined. */
function defined(attributes: Attributes): Record<string, AttributeValue> {
  const result: Record<string, AttributeValue> = {};
  for (const [key, value] of Object.entries(attributes))
    if (value !== undefined)
      result[key] = value;
  return result;
}

/** A span in progress; `end` hands it to the batch. */
export class BrowserSpan implements TraceContext {
  readonly traceId: string;
  readonly spanId = randomId(8);
  readonly parentSpanId?: string;
  readonly start = epochMs();
  private readonly attributes: Attributes;
  private status: SpanStatus = 'unset';
  private ended = false;

  constructor(private name: string, readonly kind: SpanKind, parent: TraceContext | undefined, attributes: Attributes,
    private readonly done: (span: SpanData) => void) {
    this.traceId = parent?.traceId ?? randomId(16);
    this.parentSpanId = parent?.spanId;
    this.attributes = { ...attributes };
  }

  /** The traceparent a call made in this span sends, so the server's spans join it. */
  get traceparent(): string {
    return formatTraceparent(this);
  }

  get isEnded(): boolean {
    return this.ended;
  }

  setName(name: string): void {
    this.name = name;
  }

  setAttribute(key: string, value: AttributeValue | undefined): void {
    this.attributes[key] = value;
  }

  setStatus(status: SpanStatus): void {
    this.status = status;
  }

  end(): void {
    if (this.ended)
      return;
    this.ended = true;
    this.done({
      traceId: this.traceId,
      spanId: this.spanId,
      parentSpanId: this.parentSpanId,
      name: this.name.slice(0, MAX_SPAN_NAME),
      kind: this.kind,
      start: this.start,
      end: epochMs(),
      status: this.status,
      attributes: defined(this.attributes),
    });
  }
}

export interface SpanOptions {
  kind?: SpanKind;
  /** The parent: undefined for the active span (the navigation under way, if any), null for a trace of its own. */
  parent?: TraceContext | null;
  attributes?: Attributes;
}

@Injectable({ providedIn: 'root' })
export class BrowserTelemetry {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  /** Read once: the page that started the app says whether the API takes the browser's telemetry. */
  private readonly markedOn = this.browser && browserTelemetryOn();
  private readonly queue: TelemetryBatch = { spans: [], logs: [], vitals: [] };
  private activeSpan?: BrowserSpan;
  private off = false;

  constructor() {
    if (!this.browser)
      return;
    const timer = setInterval(() => this.flush(), FLUSH_INTERVAL_MS);
    // What's left goes as the page hides or goes away (fetch's keepalive outlives it)
    const flushHidden = () => {
      if (document.visibilityState === 'hidden')
        this.flush();
    };
    const flush = () => this.flush();
    addEventListener('visibilitychange', flushHidden);
    addEventListener('pagehide', flush);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(timer);
      removeEventListener('visibilitychange', flushHidden);
      removeEventListener('pagehide', flush);
    });
  }

  /**
   * Whether anything is recorded: in a browser whose page says browser telemetry is on, not under automation
   * (navigator.webdriver), and not turned off by the API since (404).
   */
  get enabled(): boolean {
    return this.markedOn && !navigator.webdriver && !this.off;
  }

  startSpan(name: string, options: SpanOptions = {}): BrowserSpan {
    const parent = options.parent === undefined ? this.active() : options.parent ?? undefined;
    return new BrowserSpan(name, options.kind ?? 'internal', parent, options.attributes ?? {}, span => {
      if (this.enabled)
        this.push(this.queue.spans, span);
    });
  }

  /** The span calls and log lines join when they name none: the navigation under way, while it is. */
  active(): BrowserSpan | undefined {
    return this.activeSpan?.isEnded ? undefined : this.activeSpan;
  }

  setActive(span: BrowserSpan | undefined): void {
    this.activeSpan = span;
  }

  /** A log line, in `trace` or else the active span's trace. */
  log(level: BrowserLogLevel, message: string, attributes: Attributes = {}, trace: TraceContext | undefined = this.active()): void {
    if (!this.enabled)
      return;
    this.push(this.queue.logs, {
      time: epochMs(),
      level,
      message: (message || '(empty)').slice(0, MAX_LOG_MESSAGE),
      traceId: trace?.traceId,
      spanId: trace?.spanId,
      attributes: defined(attributes),
    });
  }

  /** A Web Vital; one outside the API's range (a tab left open for days) is dropped rather than refuse its batch. */
  vital(name: VitalData['name'], value: number): void {
    if (this.enabled && Number.isFinite(value) && value >= 0 && value <= MAX_VITAL)
      this.push(this.queue.vitals, { name, value });
  }

  /** Sends what is queued, up to MAX_BATCH of each. */
  flush(): void {
    if (!this.enabled) {
      this.queue.spans.length = this.queue.logs.length = this.queue.vitals.length = 0;
      return;
    }
    const batch: TelemetryBatch = {
      spans: this.queue.spans.splice(0, MAX_BATCH),
      logs: this.queue.logs.splice(0, MAX_BATCH),
      vitals: this.queue.vitals.splice(0, MAX_BATCH),
    };
    if (!batch.spans.length && !batch.logs.length && !batch.vitals.length)
      return;
    const body = JSON.stringify(batch);
    fetch(TELEMETRY_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: new TextEncoder().encode(body).length <= MAX_KEEPALIVE_BYTES,
      credentials: 'same-origin',
    }).then(response => {
      // 404: telemetry is off here. Any other refusal (a 400 or 413 for one batch, a 429) drops that batch only
      if (response.status === 404)
        this.off = true;
    }).catch(() => {
      // Telemetry is best effort: a batch that doesn't go is dropped
    });
  }

  private push<T>(list: T[], item: T): void {
    list.push(item);
    if (list.length > MAX_QUEUE)
      list.splice(0, list.length - MAX_QUEUE);
  }
}
