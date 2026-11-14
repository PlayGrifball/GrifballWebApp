import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateBracketDialogStore } from './createBracketDialog.store';

describe('CreateBracketDialogStore', () => {
  let store: CreateBracketDialogStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [CreateBracketDialogStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(CreateBracketDialogStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("makes the season's bracket, then says it is made", () => {
    const created = jasmine.createSpy('created');
    store.create(3, { participantsCount: 4, doubleElimination: false, bestOf: 5 }, created);
    http.expectOne({ method: 'POST', url: '/api/seasons/3/bracket?participantsCount=4&doubleElimination=false&bestOf=5' }).flush({});
    expect(created).toHaveBeenCalledTimes(1);
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('says when it could not be made', () => {
    spyOn(console, 'log');
    const created = jasmine.createSpy('created');
    store.create(3, { participantsCount: 4, doubleElimination: false, bestOf: 5 }, created);
    http.expectOne(r => r.url === '/api/seasons/3/bracket').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(created).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to create bracket', 'Close');
  });
});
