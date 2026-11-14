import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { CommissionerDashboardDto } from '@api/models';
import { CommissionerDashboardStore } from './commissionerDashboard.store';

const data = { pendingReschedules: [] } as unknown as CommissionerDashboardDto;

describe('CommissionerDashboardStore', () => {
  let store: CommissionerDashboardStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [CommissionerDashboardStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(CommissionerDashboardStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the dashboard, and says when it fails, clearing that on a retry', () => {
    spyOn(console, 'error');
    expect(store.loading()).toBeTrue();
    store.load();
    http.expectOne('/api/commissioner-dashboard').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loading()).toBeFalse();
    expect(store.error()).toBe('Failed to load dashboard data');

    store.load();
    expect(store.loading()).toBeTrue();
    expect(store.error()).toBeNull();
    http.expectOne('/api/commissioner-dashboard').flush(data);
    expect(store.loading()).toBeFalse();
    expect(store.dashboardData()).toEqual(data);
  });

  it("makes a request's Discord thread, then loads the dashboard again, whatever the answer", () => {
    spyOn(console, 'error');
    store.createDiscordThread(7);
    http.expectOne({ method: 'POST', url: '/api/reschedules/7/discord-thread' }).flush({});
    http.expectOne('/api/commissioner-dashboard').flush(data);

    store.createDiscordThread(7);
    http.expectOne('/api/reschedules/7/discord-thread').flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne('/api/commissioner-dashboard').flush(data);
    expect(console.error).toHaveBeenCalled();
  });
});
