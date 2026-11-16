// What dashboard-grafana.mjs counts as a panel's real data. Apart from the script, which runs on import, so
// tests/dashboard.test.mjs checks these rules on made-up results.

/**
 * The panels a release with some traffic always fills (e2e/dashboard.sh makes it): each must return real series. The
 * rest need what CI can't make (visitors' browsers, chats, Halo, jobs that run hourly, failures) and only have to run.
 */
export const REQUIRED = new Set([
  'Requests', 'Server errors', 'p95 latency', 'Unhealthy checks', 'Live connections', 'Exceptions',
  'Requests by route', 'Requests by status', 'p95 latency by route',
  'EF Core', 'Compiled query cache',
  'Server rendering', 'Frontend memory',
  'CPU', 'Memory', 'GC heap and pauses', 'Thread pool queue', 'Lock contention',
  'Errors and warnings',
]);

/** Labels a series must carry when its panel's legend shows them: the ones a wrong name would leave empty. */
export const SERIES_LABELS = ['k8s_pod_name'];

/**
 * A required panel's query as it's checked: without its zero fills (`or vector(0)`), which keep a stat at 0 when there
 * is nothing to count but would answer even if the series it counts were renamed or gone. As the generator writes them.
 */
export const requiredQuery = expr => expr.replaceAll(' or vector(0)', '');

/** The labels of each series in a Grafana /api/ds/query result. */
export const seriesLabels = result =>
  (result?.frames ?? []).flatMap(f => (f.schema?.fields ?? []).filter(field => field.name !== 'Time' && field.type !== 'time').map(field => field.labels ?? {}));

/** Data points across a /api/ds/query result's frames. */
export const points = result => (result?.frames ?? []).reduce((n, f) => n + (f.data?.values?.[0]?.length ?? 0), 0);

/** A required target's series that lack a label its legend shows (SERIES_LABELS), as failure messages. */
export function seriesFailures(target, result) {
  const wanted = SERIES_LABELS.filter(label => target.legendFormat?.includes(`{{${label}}}`));
  return seriesLabels(result).flatMap(labels => wanted.filter(label => !labels[label])
    .map(label => `${target.refId}: a series without ${label} (${JSON.stringify(labels)})`));
}
