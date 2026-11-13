// What the app's server (core/server/serverTelemetry.ts) and the browser's telemetry (browserTelemetry.ts) both
// name: kept here so browser code needs nothing from core/server.

/**
 * The span attribute that says what a request of the app's server is (a RequestKind): the backend's Telemetry page
 * reads it (TelemetryQueries.TraceRow) to tell a page's trace from an API call's, rather than read span names.
 */
export const REQUEST_KIND_ATTRIBUTE = 'grif.request.kind';

/**
 * The Server-Timing entry that tells the page browser telemetry is on (the API takes it): without it the browser
 * records nothing, and sends no traceparent whose span would never arrive (core/telemetry/browserTelemetry.ts).
 */
export const BROWSER_TELEMETRY_MARK = 'browser-telemetry';

/** The browser's telemetry batches: every few seconds a tab, never traced (the backend leaves them out too). */
export const CLIENT_TELEMETRY_PATH = '/api/client-telemetry';
