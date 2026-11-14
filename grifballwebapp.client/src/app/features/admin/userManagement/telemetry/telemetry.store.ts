import { inject, Injectable, type Resource } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { LogsDto, MetricsDto, TelemetryStatusDto, TraceDto, TraceSummaryDto } from '@api/models';
import { TelemetryService } from '@api/services/telemetry.service';
import { StoreInput } from '@shared/storeInput';

export type MetricsQuery = NonNullable<Parameters<TelemetryService['getTelemetryMetrics']>[0]>;
export type LogsQuery = NonNullable<Parameters<TelemetryService['getTelemetryLogs']>[0]>;
export type TracesQuery = NonNullable<Parameters<TelemetryService['getTelemetryTraces']>[0]>;

/**
 * This app's telemetry as the server reads it back: which backends it has, and the metrics, logs, traces and a trace,
 * each read when the page asks for it (undefined: not now) and again on a refresh. The template reads each one's
 * value, error and loading state, so they are given as read-only resources. One per page (the component provides it).
 */
@Injectable()
export class TelemetryStore {
  private readonly api = inject(TelemetryService);

  /** What to read (undefined: nothing): the component's tab, range and filters. */
  readonly metricsQuery = new StoreInput<MetricsQuery | undefined>();
  readonly logsQuery = new StoreInput<LogsQuery | undefined>();
  readonly tracesQuery = new StoreInput<TracesQuery | undefined>();
  readonly traceId = new StoreInput<string | null>();

  private readonly _status = rxResource({ stream: () => this.api.getTelemetryStatus() });
  private readonly _metrics = rxResource({ params: () => this.metricsQuery.value(), stream: ({ params }) => this.api.getTelemetryMetrics(params) });
  private readonly _logs = rxResource({ params: () => this.logsQuery.value(), stream: ({ params }) => this.api.getTelemetryLogs(params) });
  private readonly _traces = rxResource({ params: () => this.tracesQuery.value(), stream: ({ params }) => this.api.getTelemetryTraces(params) });
  private readonly _traceDetail = rxResource({
    params: () => this.traceId.value() ?? undefined,
    stream: ({ params }) => this.api.getTelemetryTrace({ traceID: params }),
  });

  readonly status: Resource<TelemetryStatusDto | undefined> = this._status;
  readonly metrics: Resource<MetricsDto | undefined> = this._metrics;
  readonly logs: Resource<LogsDto | undefined> = this._logs;
  readonly traces: Resource<TraceSummaryDto[] | undefined> = this._traces;
  readonly traceDetail: Resource<TraceDto | undefined> = this._traceDetail;

  reloadMetrics(): void {
    this._metrics.reload();
  }

  reloadLogs(): void {
    this._logs.reload();
  }

  /** The traces again, and the open trace when there is one. */
  reloadTraces(): void {
    this._traces.reload();
    if (this.traceId.value())
      this._traceDetail.reload();
  }
}
