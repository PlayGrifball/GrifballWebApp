import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AppLog, causeAttributes, MAX_LOGS } from './appLog';
import { BrowserTelemetry, TraceContext } from './browserTelemetry';

describe('AppLog', () => {
  let log: AppLog;
  let telemetry: jasmine.SpyObj<BrowserTelemetry>;

  beforeEach(() => {
    telemetry = jasmine.createSpyObj<BrowserTelemetry>('BrowserTelemetry', ['log', 'active']);
    TestBed.configureTestingModule({ providers: [{ provide: BrowserTelemetry, useValue: telemetry }] });
    log = TestBed.inject(AppLog);
  });

  it('writes each level to its console method, and all but debug to the telemetry', () => {
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
    expect(telemetry.log).toHaveBeenCalledWith('error', 'Failed to load', jasmine.objectContaining({ 'exception.message': 'Error: down' }), undefined);
    expect(telemetry.log).toHaveBeenCalledWith('warn', 'Retrying', {}, undefined);
    expect(telemetry.log).toHaveBeenCalledWith('info', 'Saved', { 'season.id': 4 }, undefined);
    expect(telemetry.log).toHaveBeenCalledTimes(3);
  });

  it("leaves a failed call to the telemetry's call reports: the console only", () => {
    const error = spyOn(console, 'error');
    const cause = new HttpErrorResponse({ status: 500, url: 'https://grifball.gurnt.com/api/seasons' });

    log.error('Failed to load the seasons', cause);

    expect(error).toHaveBeenCalledWith('Failed to load the seasons', cause);
    expect(telemetry.log).not.toHaveBeenCalled();
  });

  it('sends a line once a page, however often it is written', () => {
    const warn = spyOn(console, 'warn');

    for (let i = 0; i < 3; i++)
      log.warn("Couldn't start the hub", new Error('refused'));
    log.warn("Couldn't start the hub", new Error('timed out'));

    expect(warn).toHaveBeenCalledTimes(4);
    expect(telemetry.log).toHaveBeenCalledTimes(2);
  });

  it(`sends at most ${MAX_LOGS} lines a page`, () => {
    spyOn(console, 'info');

    for (let i = 0; i < MAX_LOGS + 5; i++)
      log.info(`Step ${i}`);

    expect(telemetry.log).toHaveBeenCalledTimes(MAX_LOGS);
  });

  it("puts the line in the trace of what the user is doing", () => {
    spyOn(console, 'warn');
    const trace: TraceContext = { traceId: 'a'.repeat(32), spanId: 'b'.repeat(16) };
    telemetry.active.and.returnValue(trace as ReturnType<BrowserTelemetry['active']>);

    log.warn('Retrying');

    expect(telemetry.log).toHaveBeenCalledWith('warn', 'Retrying', {}, jasmine.objectContaining({ traceId: trace.traceId, spanId: trace.spanId }));
  });
});

describe('causeAttributes', () => {
  it("gives an error's message and stack, as the error reports do, with sensitive query values redacted", () => {
    const cause = new TypeError('fetch https://grifball.gurnt.com/x?token=abc failed');
    const attributes = causeAttributes(cause);
    expect(attributes['exception.message']).toContain('TypeError: fetch ');
    expect(attributes['exception.message']).toContain('token=REDACTED');
    expect(attributes['exception.message']).not.toContain('abc');
    expect(attributes['exception.stacktrace']).toEqual(jasmine.any(String));
  });

  it('gives anything else as text, and nothing for no cause', () => {
    expect(causeAttributes('refused')).toEqual({ 'error.detail': 'refused' });
    expect(causeAttributes({ status: 409 })).toEqual({ 'error.detail': '{"status":409}' });
    expect(causeAttributes(undefined)).toEqual({});
  });
});
