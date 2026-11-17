import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProcessRescheduleDialogStore } from './processRescheduleDialog.store';

describe('ProcessRescheduleDialogStore', () => {
  let store: ProcessRescheduleDialogStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ProcessRescheduleDialogStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ProcessRescheduleDialogStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the decision, and says it is taken', () => {
    const done = jasmine.createSpy('done');
    store.process(55, { approved: true, commissionerNotes: 'ok' }, done);
    expect(store.processing()).toBeTrue();
    const send = http.expectOne({ method: 'POST', url: '/api/reschedules/55/decision' });
    expect(send.request.body).toEqual({ approved: true, commissionerNotes: 'ok' });
    send.flush({});
    expect(done).toHaveBeenCalledTimes(1);
    expect(store.processing()).toBeFalse();
    expect(store.error()).toBeNull();
  });

  it("keeps the server's reason when it refuses, and clears it on a new try", () => {
    const done = jasmine.createSpy('done');
    store.process(55, { approved: true, commissionerNotes: null }, done);
    http.expectOne('/api/reschedules/55/decision').flush({ title: 'Refused', detail: 'This match already has a result.' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    expect(done).not.toHaveBeenCalled();
    expect(store.processing()).toBeFalse();
    expect(store.error()).toBe('This match already has a result.');

    store.process(55, { approved: false, commissionerNotes: null }, done);
    expect(store.error()).toBeNull();
    http.expectOne('/api/reschedules/55/decision').flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.error()).toBe('The request could not be processed. Try again.');
  });

  it('checks the requested time and loads the best others; no check without a time', () => {
    const clash = { kind: 'TeamPlaysThen' as const, message: 'Reds play another match then.', refuses: true };
    store.loadSchedule(9, '2026-03-02T00:00:00Z');
    const check = http.expectOne(r => r.url === '/api/season-matches/9/time-check');
    expect(check.request.params.get('at')).toBe('2026-03-02T00:00:00Z');
    check.flush([clash]);
    http.expectOne(r => r.url === '/api/season-matches/9/time-suggestions').flush([]);
    expect(store.conflicts()).toEqual([clash]);
    expect(store.refused()).toBeTrue();
    expect(store.alternatives()).toEqual([]);

    store.loadSchedule(9, null);
    http.expectNone(r => r.url === '/api/season-matches/9/time-check');
    http.expectOne(r => r.url === '/api/season-matches/9/time-suggestions').flush(null, { status: 500, statusText: 'Error' });
    expect(store.conflicts()).toBeNull();
    expect(store.refused()).toBeFalse();
    expect(store.alternatives()).toEqual([]);
  });
});
