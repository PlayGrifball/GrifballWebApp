import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SeedOrderingDialogStore } from './seedOrderingDialog.store';

describe('SeedOrderingDialogStore', () => {
  let store: SeedOrderingDialogStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    spyOn(console, 'log');
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [SeedOrderingDialogStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(SeedOrderingDialogStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the standings, and says when they cannot be', () => {
    expect(store.loading()).toBeTrue();
    store.load(3);
    http.expectOne('/api/seasons/3/standings').flush([{ teamID: 10, teamName: 'Alpha', wins: 4, losses: 1 }]);
    expect(store.loading()).toBeFalse();
    expect(store.standings().map(t => t.teamName)).toEqual(['Alpha']);

    store.load(3);
    http.expectOne('/api/seasons/3/standings').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to load team standings', 'Close');
  });

  it('sets the seeds, and says when they cannot be', () => {
    const done = jasmine.createSpy('done');
    store.setSeeds(3, [{ teamID: 10, seed: 1 }], done);
    const send = http.expectOne({ method: 'POST', url: '/api/seasons/3/bracket/seeds' });
    expect(send.request.body).toEqual([{ teamID: 10, seed: 1 }]);
    send.flush({});
    expect(done).toHaveBeenCalledTimes(1);

    store.setSeeds(3, [], done);
    http.expectOne('/api/seasons/3/bracket/seeds').flush('x', { status: 500, statusText: 'Server Error' });
    expect(done).toHaveBeenCalledTimes(1);
    expect(snackBar.open).toHaveBeenCalledWith('Failed to set custom seeds', 'Close');
  });
});
