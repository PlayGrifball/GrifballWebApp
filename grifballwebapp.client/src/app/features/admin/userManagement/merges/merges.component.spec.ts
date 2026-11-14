import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MergesComponent } from './merges.component';
import type { UserMergeDto } from '@api/models';

function merge(id: number, changes: Partial<UserMergeDto> = {}): UserMergeDto {
  return {
    userMergeID: id, mergedAt: '2026-11-01T12:00:00Z', trigger: 'ClaimApproval', reason: 'claim 12', triggeredByUserID: 1, triggeredByUserName: 'admin',
    sourceUserID: 50, sourceUserName: 'historical:Old Tag', targetUserID: 7, targetUserName: 'noah', rowsMoved: 3, rowsRemoved: 2, rowsAdded: 1,
    undoneAt: null, undoneByUserID: null, undoNote: null, ...changes,
  };
}

describe('MergesComponent', () => {
  let fixture: ComponentFixture<MergesComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: MatSnackBar, useValue: snackBar }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MergesComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tbody tr'));
  const button = (root: Element, text: string) => Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === text)!;

  function listRequest(page: number): TestRequest {
    const req = http.expectOne(r => r.url === '/api/user-merges');
    expect(req.request.params.get('pageNumber')).toBe(String(page));
    expect(req.request.params.get('pageSize')).toBe('25');
    return req;
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('lists the merges, newest first, with why and what they changed', async () => {
    listRequest(1).flush({ totalCount: 2, results: [merge(2, { trigger: 'Manual', reason: null, triggeredByUserName: null, triggeredByUserID: null }), merge(1, { trigger: 'Unknown' })] });
    await settle();
    expect(Array.from(el().querySelectorAll('th')).every(th => th.getAttribute('scope') === 'col')).toBeTrue();
    const [first, second] = rows();
    expect(first.textContent).toContain('Manual merge');
    expect(first.textContent).toContain('System');
    expect(first.textContent).toContain('3 moved · 2 removed · 1 added');
    expect(first.querySelector('a')!.getAttribute('href')).toBe('/profile/7');
    expect(second.textContent).toContain('Unknown');
    expect(second.textContent).toContain('claim 12');
    expect(second.textContent).toContain('admin');
    expect(button(first, 'Undo').getAttribute('aria-label')).toBe('Undo the merge of historical:Old Tag');
    expect(el().querySelector('.merges-pager')).toBeNull();
    expect(el().querySelector('.merges-links a')!.getAttribute('href')).toBe('/usermanagement/claims');
  });

  it('undoes a merge and reloads the list', async () => {
    listRequest(1).flush({ totalCount: 1, results: [merge(1)] });
    await settle();
    button(rows()[0], 'Undo').click();
    fixture.detectChanges();
    expect(button(rows()[0], 'Undo').disabled).toBeTrue();
    const undo = http.expectOne('/api/user-merges/1/undo');
    expect(undo.request.body).toEqual({ note: null });
    undo.flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Undid the merge of historical:Old Tag.', 'Close');
    fixture.detectChanges();
    listRequest(1).flush({ totalCount: 1, results: [merge(1, { undoneAt: '2026-11-01T13:00:00Z', undoNote: 'wrong person' })] });
    await settle();
    expect(rows()[0].textContent).toContain('Undone');
    expect(rows()[0].textContent).toContain('wrong person');
    expect(rows()[0].querySelector('button')).toBeNull();
  });

  it('shows why an undo was refused on its row', async () => {
    listRequest(1).flush({ totalCount: 1, results: [merge(1, { sourceUserName: null })] });
    await settle();
    button(rows()[0], 'Undo').click();
    http.expectOne('/api/user-merges/1/undo').flush({ title: 'Conflict', detail: 'Merge 4 since changed the same rows: undo it first.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(rows()[0].querySelector('[role="alert"]')!.textContent).toContain('Merge 4 since changed the same rows');
    expect(snackBar.open).toHaveBeenCalledWith('Merge 4 since changed the same rows: undo it first.', 'Close');
    expect(button(rows()[0], 'Undo').disabled).toBeFalse();
  });

  it('pages through older merges', async () => {
    listRequest(1).flush({ totalCount: 30, results: [merge(30)] });
    await settle();
    expect(el().querySelector('.merges-pager')!.textContent).toContain('Page 1 of 2');
    expect(button(el(), 'Newer').disabled).toBeTrue();
    button(el(), 'Older').click();
    fixture.detectChanges();
    listRequest(2).flush({ totalCount: 30, results: [merge(5)] });
    await settle();
    expect(button(el(), 'Older').disabled).toBeTrue();
    button(el(), 'Newer').click();
    fixture.detectChanges();
    listRequest(1).flush({ totalCount: 30, results: [merge(30)] });
    await settle();
  });

  it('says when there are none', async () => {
    listRequest(1).flush({ totalCount: 0, results: [] });
    await settle();
    expect(el().querySelector('.hud-empty')!.textContent).toContain('No merges yet.');
  });

  it('says when they cannot be loaded', async () => {
    listRequest(1).flush('', { status: 500, statusText: 'down' });
    await settle();
    expect(el().querySelector('[role="alert"]')!.textContent).toContain("couldn't be loaded");
  });
});
