import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AppLog, causeAttributes, MAX_CAUSE } from './appLog';
import { BrowserTelemetry } from './browserTelemetry';

describe('AppLog', () => {
  let log: AppLog;
  let telemetry: jasmine.SpyObj<BrowserTelemetry>;

  beforeEach(() => {
    telemetry = jasmine.createSpyObj<BrowserTelemetry>('BrowserTelemetry', ['log']);
    TestBed.configureTestingModule({ providers: [{ provide: BrowserTelemetry, useValue: telemetry }] });
    log = TestBed.inject(AppLog);
  });

  it('writes each level to its console method and to the telemetry, with the cause', () => {
    const error = spyOn(console, 'error');
    const warn = spyOn(console, 'warn');
    const info = spyOn(console, 'info');
    const debug = spyOn(console, 'debug');
    const cause = new Error('down');

    log.error('Failed to load', cause);
    log.warn('Retrying');
    log.info('Saved', undefined, { 'season.id': 4 });
    log.debug('Picked');

    expect(error).toHaveBeenCalledWith('Failed to load', cause);
    expect(warn).toHaveBeenCalledWith('Retrying');
    expect(info).toHaveBeenCalledWith('Saved');
    expect(debug).toHaveBeenCalledWith('Picked');
    expect(telemetry.log).toHaveBeenCalledWith('error', 'Failed to load', { 'exception.type': 'Error', 'exception.message': 'down' });
    expect(telemetry.log).toHaveBeenCalledWith('warn', 'Retrying', {});
    expect(telemetry.log).toHaveBeenCalledWith('info', 'Saved', { 'season.id': 4 });
    expect(telemetry.log).toHaveBeenCalledWith('debug', 'Picked', {});
  });
});

describe('causeAttributes', () => {
  it('gives an HTTP answer\'s status, path and message', () => {
    const cause = new HttpErrorResponse({ status: 404, statusText: 'Not Found', url: 'https://grifball.gurnt.com/api/teams/7?x=1' });
    expect(causeAttributes(cause)).toEqual({
      'http.response.status_code': 404,
      'url.path': '/api/teams/7',
      'error.message': cause.message,
    });
  });

  it('leaves the path out of an answer without a URL', () => {
    expect(causeAttributes(new HttpErrorResponse({ status: 0 }))['url.path']).toBeUndefined();
  });

  it('leaves the path out of an answer whose URL does not parse', () => {
    expect(causeAttributes(new HttpErrorResponse({ status: 502, url: 'https://[' }))['url.path']).toBeUndefined();
  });

  it('gives an error\'s type and message, cut to the limit', () => {
    expect(causeAttributes(new TypeError('x'.repeat(MAX_CAUSE + 5)))).toEqual({ 'exception.type': 'TypeError', 'exception.message': 'x'.repeat(MAX_CAUSE) });
  });

  it('gives anything else as text, and nothing for no cause', () => {
    expect(causeAttributes('refused')).toEqual({ 'error.detail': 'refused' });
    expect(causeAttributes({ status: 409 })).toEqual({ 'error.detail': '{"status":409}' });
    const circular: Record<string, unknown> = {};
    circular['self'] = circular;
    expect(causeAttributes(circular)).toEqual({ 'error.detail': '[object Object]' });
    expect(causeAttributes(undefined)).toEqual({});
  });
});
