import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateRegularMatchesDialogStore } from './createRegularMatchesDialog.store';

describe('CreateRegularMatchesDialogStore', () => {
  let store: CreateRegularMatchesDialogStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [CreateRegularMatchesDialogStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(CreateRegularMatchesDialogStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("makes the season's regular matches, then says they are made", () => {
    const created = jasmine.createSpy('created');
    store.create(11, 2, 3, created);
    http.expectOne({ method: 'POST', url: '/api/seasons/11/matches/generate?homeMatchesPerTeam=2&bestOf=3' }).flush({});
    expect(created).toHaveBeenCalledTimes(1);
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('says when they could not be made', () => {
    spyOn(console, 'log');
    const created = jasmine.createSpy('created');
    store.create(11, 2, 3, created);
    http.expectOne(r => r.url === '/api/seasons/11/matches/generate').flush('x', { status: 500, statusText: 'Server Error' });
    expect(created).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to create regular matches', 'Close');
  });
});
