import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RescheduleRequestStore } from './rescheduleRequest.store';

describe('RescheduleRequestStore', () => {
  let store: RescheduleRequestStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  const request = { seasonMatchID: 31, newScheduledTime: null, reason: 'Out of town' };

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [RescheduleRequestStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(RescheduleRequestStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the request, says so, and says it is sent', () => {
    const sent = jasmine.createSpy('sent');
    store.submit(request, sent);
    expect(store.submitting()).toBeTrue();
    const send = http.expectOne({ method: 'POST', url: '/api/season-matches/31/reschedules' });
    expect(send.request.body).toEqual(request);
    send.flush({});
    expect(sent).toHaveBeenCalledTimes(1);
    expect(store.submitting()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('Reschedule request submitted successfully!', 'Close', { duration: 5000 });
  });

  it("says the server's reason when it refuses", () => {
    spyOn(console, 'error');
    const sent = jasmine.createSpy('sent');
    store.submit(request, sent);
    http.expectOne('/api/season-matches/31/reschedules').flush({ title: 'Refused', detail: 'A request is already active.' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    expect(sent).not.toHaveBeenCalled();
    expect(store.submitting()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('A request is already active.', 'Close', { duration: 5000 });
  });
});
