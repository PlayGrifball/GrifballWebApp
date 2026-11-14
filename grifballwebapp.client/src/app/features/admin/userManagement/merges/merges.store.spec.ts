import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { UserMergeDto } from '@api/models';
import { MergesStore } from './merges.store';

const merge = (userMergeID: number, sourceUserName: string | null = 'Old Tag') => ({ userMergeID, sourceUserID: 50, sourceUserName }) as UserMergeDto;

describe('MergesStore', () => {
  let store: MergesStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [MergesStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(MergesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const list = (page: number) => {
    TestBed.tick();
    return http.expectOne(r => r.url === '/api/user-merges' && r.params.get('pageNumber') === String(page) && r.params.get('pageSize') === '25');
  };
  const problem = { title: 'Refused', detail: 'Already undone.' };
  const refused = { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } };

  it('loads the merges a page at a time, within the pages there are', async () => {
    list(1).flush({ totalCount: 30, results: [merge(1)] });
    await settle();
    expect(store.rows()).toHaveSize(1);
    expect(store.pages()).toBe(2);
    expect(store.empty()).toBeFalse();

    store.previous();
    expect(store.page()).toBe(1);
    store.next();
    expect(store.page()).toBe(2);
    list(2).flush({ totalCount: 30, results: [merge(30)] });
    await settle();
  });

  it('says when there are none, and when they cannot be loaded', async () => {
    list(1).flush('', { status: 500, statusText: 'down' });
    await settle();
    expect(store.failed()).toBeTrue();
    expect(store.rows()).toEqual([]);
    expect(store.pages()).toBe(1);
  });

  it('undoes a merge, says so, and loads the page again', async () => {
    list(1).flush({ totalCount: 1, results: [merge(1)] });
    await settle();
    store.undo(merge(1));
    expect(store.undoing()).toBe(1);
    const undo = http.expectOne({ method: 'POST', url: '/api/user-merges/1/undo' });
    expect(undo.request.body).toEqual({ note: null });
    undo.flush(null);
    expect(store.undoing()).toBeNull();
    expect(store.refusals()[1]).toBe('');
    expect(snackBar.open).toHaveBeenCalledWith('Undid the merge of Old Tag.', 'Close');
    list(1).flush({ totalCount: 0, results: [] });
    await settle();
    expect(store.empty()).toBeTrue();
  });

  it("keeps the server's reason when it refuses an undo, and names a user without a name by ID", async () => {
    list(1).flush({ totalCount: 1, results: [merge(1, null)] });
    await settle();
    store.undo(merge(1, null));
    http.expectOne('/api/user-merges/1/undo').flush(problem, refused);
    expect(store.undoing()).toBeNull();
    expect(store.refusals()[1]).toBe('Already undone.');
    expect(snackBar.open).toHaveBeenCalledWith('Already undone.', 'Close');

    store.undo(merge(1, null));
    http.expectOne('/api/user-merges/1/undo').flush(null);
    expect(snackBar.open).toHaveBeenCalledWith('Undid the merge of user 50.', 'Close');
    list(1).flush({ totalCount: 0, results: [] });
    await settle();
  });
});
