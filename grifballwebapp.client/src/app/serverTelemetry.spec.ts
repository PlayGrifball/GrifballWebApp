import {
  DEFAULT_METRIC_INTERVAL_MS,
  DEFAULT_SERVICE_NAME,
  MAX_METRIC_INTERVAL_MS,
  MIN_METRIC_INTERVAL_MS,
  errorFields,
  formatTraceparent,
  isLogged,
  isProbe,
  isTraced,
  logLevel,
  logLine,
  metricInterval,
  otlpEndpoint,
  parseResourceAttributes,
  requestKind,
  requestLogLevel,
  routeOf,
  serverTelemetrySettings,
  serverTimingHeader,
  spanName,
} from './serverTelemetry';

describe('server telemetry', () => {
  describe('settings', () => {
    it('reads the OpenTelemetry variables', () => {
      const settings = serverTelemetrySettings({
        OTEL_EXPORTER_OTLP_ENDPOINT: 'http://collector:4318/',
        OTEL_SERVICE_NAME: ' grif-test ',
        OTEL_RESOURCE_ATTRIBUTES: 'deployment.environment.name=test,team=a%20b',
        OTEL_METRIC_EXPORT_INTERVAL: '10000',
        HOSTNAME: 'grif-frontend-abc',
        LOG_LEVEL: 'DEBUG',
      });

      expect(settings).toEqual({
        endpoint: 'http://collector:4318',
        serviceName: 'grif-test',
        resource: {
          'deployment.environment.name': 'test',
          team: 'a b',
          'service.name': 'grif-test',
          'service.component': 'frontend',
          'service.instance.id': 'grif-frontend-abc',
        },
        metricIntervalMs: 10_000,
        logLevel: 'debug',
      });
    });

    it('exports nothing, and logs at info, with none of them set', () => {
      const settings = serverTelemetrySettings({});

      expect(settings.endpoint).toBeNull();
      expect(settings.serviceName).toBe(DEFAULT_SERVICE_NAME);
      expect(settings.resource['service.instance.id']).toBeUndefined();
      expect(settings.metricIntervalMs).toBe(DEFAULT_METRIC_INTERVAL_MS);
      expect(settings.logLevel).toBe('info');
    });

    it('keeps service.name and service.component its own, whatever OTEL_RESOURCE_ATTRIBUTES says', () => {
      const settings = serverTelemetrySettings({ OTEL_SERVICE_NAME: 'grif', OTEL_RESOURCE_ATTRIBUTES: 'service.name=other,service.component=x' });

      expect(settings.resource['service.name']).toBe('grif');
      expect(settings.resource['service.component']).toBe('frontend');
    });

    it('skips resource attributes without a key or a value, and keeps a value that is not URL-encoded', () => {
      expect(parseResourceAttributes('=a,b=,c,d=%zz, e = f ')).toEqual({ d: '%zz', e: 'f' });
      expect(parseResourceAttributes(undefined)).toEqual({});
    });

    it('takes only an http(s) collector', () => {
      expect(otlpEndpoint('https://collector.example:4318//')).toBe('https://collector.example:4318');
      expect(otlpEndpoint('ftp://collector')).toBeNull();
      expect(otlpEndpoint('collector:4318')).toBeNull();
      expect(otlpEndpoint('not a url')).toBeNull();
      expect(otlpEndpoint('  ')).toBeNull();
      expect(otlpEndpoint(undefined)).toBeNull();
    });

    it('keeps the metric interval between 5 seconds and 5 minutes', () => {
      expect(metricInterval('1')).toBe(MIN_METRIC_INTERVAL_MS);
      expect(metricInterval('999999999')).toBe(MAX_METRIC_INTERVAL_MS);
      expect(metricInterval('abc')).toBe(DEFAULT_METRIC_INTERVAL_MS);
      expect(metricInterval(' ')).toBe(DEFAULT_METRIC_INTERVAL_MS);
    });

    it('logs at info for a level it does not know', () => {
      expect(logLevel('verbose')).toBe('info');
      expect(logLevel(' Warn ')).toBe('warn');
    });
  });

  it('logs a level at or above the lowest', () => {
    expect(isLogged('error', 'info')).toBeTrue();
    expect(isLogged('info', 'info')).toBeTrue();
    expect(isLogged('debug', 'info')).toBeFalse();
  });

  describe('requests', () => {
    it('tells the backend\'s paths, the build\'s files and pages apart', () => {
      expect(requestKind('/api')).toBe('api');
      expect(requestKind('/api/Season/1')).toBe('api');
      expect(requestKind('/apis')).toBe('page');
      expect(requestKind('/hub/TeamsHub')).toBe('hub');
      expect(requestKind('/signin-discord')).toBe('signin');
      expect(requestKind('/main-ABCDEFGH.js')).toBe('file');
      expect(requestKind('/media/rajdhani.woff2')).toBe('file');
      expect(requestKind('/')).toBe('page');
      expect(requestKind('/season/12')).toBe('page');
    });

    it('traces neither the build\'s files nor probes', () => {
      expect(isProbe('kube-probe/1.33')).toBeTrue();
      expect(isProbe('Mozilla/5.0')).toBeFalse();
      expect(isProbe(undefined)).toBeFalse();
      expect(isTraced('page', 'Mozilla/5.0')).toBeTrue();
      expect(isTraced('api', undefined)).toBeTrue();
      expect(isTraced('page', 'kube-probe/1.33')).toBeFalse();
      expect(isTraced('file', 'Mozilla/5.0')).toBeFalse();
    });

    it('names spans by method and route', () => {
      expect(spanName('GET', 'page')).toBe('GET /*');
      expect(spanName('POST', 'api')).toBe('POST /api/*');
      expect(routeOf('hub')).toBe('/hub/*');
      expect(routeOf('signin')).toBe('/signin-discord');
      expect(routeOf('file')).toBe('/*.*');
    });

    it('logs 5xx as errors, pages at info, and the rest at debug', () => {
      expect(requestLogLevel('api', 502)).toBe('error');
      expect(requestLogLevel('page', 200)).toBe('info');
      expect(requestLogLevel('api', 404)).toBe('debug');
    });
  });

  it('formats traceparent and Server-Timing', () => {
    const traceparent = formatTraceparent('0af7651916cd43dd8448eb211c80319c', 'b7ad6b7169203331', true);

    expect(traceparent).toBe('00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01');
    expect(formatTraceparent('0af7651916cd43dd8448eb211c80319c', 'b7ad6b7169203331', false)).toMatch(/-00$/);
    expect(serverTimingHeader(traceparent)).toBe(`traceparent;desc="${traceparent}"`);
  });

  describe('log lines', () => {
    const time = new Date('2026-11-12T10:00:00Z');

    it('are JSON, with the trace ids last', () => {
      const line = logLine(time, 'info', 'Request finished', { path: '/', status: 200, skipped: undefined },
        { traceId: '0af7651916cd43dd8448eb211c80319c', spanId: 'b7ad6b7169203331' });

      expect(line).toBe('{"time":"2026-11-12T10:00:00.000Z","level":"info","msg":"Request finished","path":"/","status":200,'
        + '"trace_id":"0af7651916cd43dd8448eb211c80319c","span_id":"b7ad6b7169203331"}');
    });

    it('never let a field replace the time, level, message or trace', () => {
      const line = JSON.parse(logLine(time, 'warn', 'm', { level: 'debug', msg: 'x', time: 'y', trace_id: 'z', span_id: 'w' }));

      expect(line).toEqual({ time: '2026-11-12T10:00:00.000Z', level: 'warn', msg: 'm' });
    });

    it('carry an error\'s type, message and stack', () => {
      const error = new TypeError('boom');
      const line = JSON.parse(logLine(time, 'error', 'failed', { error }));

      expect(line.error).toEqual(errorFields(error));
      expect(line.error.type).toBe('TypeError');
      expect(line.error.message).toBe('boom');
    });
  });
});
