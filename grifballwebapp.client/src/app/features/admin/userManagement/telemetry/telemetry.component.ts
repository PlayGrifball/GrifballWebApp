import { DatePipe, DecimalPipe, KeyValuePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal, type OnInit } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TelemetryService } from '@api/services/telemetry.service';
import { LOG_LEVELS, type LogLevel } from '../../api/telemetryQueries';
import type { LogEntryDto, SpanDto, TraceDto } from '@api/models';
import { problemText } from '../../../../shared/errorText';
import { formatValue, LineChartComponent } from './lineChart.component';

export type TelemetryTab = 'overview' | 'logs' | 'traces';

/** The time ranges offered, in minutes. */
export const RANGES: readonly { minutes: number; label: string }[] = [
  { minutes: 15, label: '15 min' },
  { minutes: 60, label: '1 h' },
  { minutes: 360, label: '6 h' },
  { minutes: 1440, label: '24 h' },
  { minutes: 10080, label: '7 days' },
];

/** A span placed in the waterfall: its depth under its parent, and its bar as percentages of the trace. */
export interface WaterfallRow {
  span: SpanDto;
  depth: number;
  left: number;
  width: number;
}

/** The trace's spans in tree order (each after its parent, siblings by start), with their bars. Spans whose parent isn't here (another service's) are roots. */
export function waterfall(trace: TraceDto): WaterfallRow[] {
  const ids = new Set(trace.spans.map(s => s.spanId));
  const children = new Map<string | null, SpanDto[]>();
  for (const span of trace.spans) {
    const parent = span.parentSpanId && ids.has(span.parentSpanId) ? span.parentSpanId : null;
    children.set(parent, [...(children.get(parent) ?? []), span]);
  }

  const total = trace.durationMs || 1;
  const rows: WaterfallRow[] = [];
  const visit = (parent: string | null, depth: number) => {
    for (const span of [...(children.get(parent) ?? [])].sort((a, b) => a.offsetMs - b.offsetMs)) {
      rows.push({ span, depth, left: span.offsetMs / total * 100, width: Math.max(span.durationMs / total * 100, 0.3) });
      visit(span.spanId, depth + 1);
    }
  };
  visit(null, 0);
  return rows;
}

/** A duration in ms as the page shows it: µs under 1 ms, s from 1000 ms. */
export function msText(ms: number): string {
  if (ms < 1)
    return `${Math.round(ms * 1000)} µs`;
  if (ms < 1000)
    return `${formatValue(ms)} ms`;
  return `${formatValue(ms / 1000)} s`;
}

/**
 * The Telemetry page (sysadmins): this app's own logs, metrics and traces, read back from Loki, Prometheus and Tempo
 * by the server, which writes every query and scopes it to this app - so it shows this app's telemetry and nothing
 * else the backends hold. A log line links to its trace, a trace to its logs.
 */
@Component({
  selector: 'app-telemetry',
  imports: [RouterLink, DatePipe, DecimalPipe, KeyValuePipe, LineChartComponent],
  templateUrl: './telemetry.component.html',
  styleUrl: './telemetry.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TelemetryComponent implements OnInit {
  private api = inject(TelemetryService);

  /** ?trace=<id> (router input binding): a link straight to a trace. */
  readonly trace = input<string | undefined>(undefined);

  readonly ranges = RANGES;
  readonly levels = LOG_LEVELS;
  readonly msText = msText;
  readonly formatValue = formatValue;
  readonly errorText = (error: unknown) => error instanceof HttpErrorResponse ? problemText(error, "Couldn't read it.") : "Couldn't read it.";

  readonly tab = signal<TelemetryTab>('overview');
  readonly minutes = signal(60);

  readonly status = rxResource({ stream: () => this.api.getTelemetryStatus() });

  /** Only the open tab reads; each reads again when its inputs change, or on Refresh. */
  readonly metrics = rxResource({
    params: () => this.tab() === 'overview' && this.status.hasValue() && this.status.value().metrics ? { minutes: this.minutes() } : undefined,
    stream: ({ params }) => this.api.getTelemetryMetrics(params),
  });
  /** An ISO time as the charts take it, in Unix milliseconds. */
  readonly ms = (iso: string) => Date.parse(iso);

  // Logs: the filters as typed; search applies on submit, not per keystroke.
  readonly pickedLevels = signal<LogLevel[]>([]);
  readonly searchDraft = signal('');
  readonly search = signal('');
  readonly traceFilter = signal('');
  readonly limit = signal(200);
  readonly logs = rxResource({
    params: () => this.tab() === 'logs' && this.status.hasValue() && this.status.value().logs
      ? { minutes: this.minutes(), level: this.pickedLevels(), search: this.search().trim() || undefined, traceId: this.traceFilter().trim() || undefined, limit: this.limit() }
      : undefined,
    stream: ({ params }) => this.api.getTelemetryLogs(params),
  });
  /** The log line opened to show its properties. */
  readonly openLine = signal<LogEntryDto | null>(null);

  // Traces: the search, and the trace open below it.
  readonly errorsOnly = signal(false);
  readonly minDurationMs = signal<number | null>(null);
  readonly traces = rxResource({
    params: () => this.tab() === 'traces' && this.status.hasValue() && this.status.value().traces
      ? { minutes: this.minutes(), errorsOnly: this.errorsOnly(), minDurationMs: this.minDurationMs() ?? undefined }
      : undefined,
    stream: ({ params }) => this.api.getTelemetryTraces(params),
  });
  readonly traceIdDraft = signal('');
  readonly traceId = signal<string | null>(null);
  readonly traceDetail = rxResource({
    params: () => this.traceId() ?? undefined,
    stream: ({ params }) => this.api.getTelemetryTrace({ traceID: params }),
  });
  readonly rows = computed(() => this.traceDetail.hasValue() ? waterfall(this.traceDetail.value()) : []);
  readonly openSpan = signal<SpanDto | null>(null);

  ngOnInit(): void {
    const linked = this.trace();
    if (linked)
      this.openTrace(linked);
  }

  refresh(): void {
    const tab = this.tab();
    if (tab === 'overview')
      this.metrics.reload();
    else if (tab === 'logs')
      this.logs.reload();
    else {
      this.traces.reload();
      if (this.traceId())
        this.traceDetail.reload();
    }
  }

  setMinutes(value: string): void {
    this.minutes.set(Number(value));
  }

  toggleLevel(level: LogLevel): void {
    this.pickedLevels.update(levels => levels.includes(level) ? levels.filter(l => l !== level) : LOG_LEVELS.filter(l => l === level || levels.includes(l)));
  }

  applySearch(event: Event): void {
    event.preventDefault();
    this.search.set(this.searchDraft().trim());
  }

  setLimit(value: string): void {
    this.limit.set(Number(value));
  }

  toggleLine(entry: LogEntryDto): void {
    this.openLine.update(open => open === entry ? null : entry);
  }

  /** The Logs tab, showing one trace's lines. */
  logsOfTrace(traceId: string): void {
    this.traceFilter.set(traceId);
    this.search.set('');
    this.searchDraft.set('');
    this.pickedLevels.set([]);
    this.openLine.set(null);
    this.tab.set('logs');
  }

  clearTraceFilter(): void {
    this.traceFilter.set('');
  }

  setMinDuration(value: string): void {
    const ms = Number(value);
    this.minDurationMs.set(value.trim() && ms > 0 ? ms : null);
  }

  /** The Traces tab, with a trace open. */
  openTrace(traceId: string): void {
    this.traceId.set(traceId.trim().toLowerCase());
    this.openSpan.set(null);
    this.tab.set('traces');
  }

  openTypedTrace(event: Event): void {
    event.preventDefault();
    if (this.traceIdDraft().trim())
      this.openTrace(this.traceIdDraft());
  }

  closeTrace(): void {
    this.traceId.set(null);
    this.openSpan.set(null);
  }

  toggleSpan(span: SpanDto): void {
    this.openSpan.update(open => open?.spanId === span.spanId ? null : span);
  }

  hasAttributes(attributes: Record<string, string>): boolean {
    return Object.keys(attributes).length > 0;
  }
}
