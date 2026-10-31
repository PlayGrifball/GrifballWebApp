import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GamertagClaimsComponent, problemText } from './gamertagClaims.component';
import { GamertagClaimDto } from '../../api/dtos/gamertagClaimDto';

function claim(id: number, changes: Partial<GamertagClaimDto> = {}): GamertagClaimDto {
  return {
    gamertagClaimID: id, kind: 'RosterMatch', status: 'Pending', xboxUserID: 2535400000000000 + id, xboxGamertag: 'Legendary117',
    userID: 117, gamertag: 'Legendary', placeholderUserID: 501, seasonID: 5, seasonName: 'Summer League 2010', teamID: 41,
    teamName: 'Relentless', createdAt: '2026-10-28T15:00:00Z', ...changes,
  };
}

const claimsUrl = '/api/GamertagClaims/GetClaims';

describe('GamertagClaimsComponent', () => {
  let fixture: ComponentFixture<GamertagClaimsComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: MatSnackBar, useValue: snackBar }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(GamertagClaimsComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tbody tr'));
  const cells = (row: Element) => Array.from(row.querySelectorAll('td')).map(td => td.textContent!.replace(/\s+/g, ' ').trim());
  const button = (row: Element, text: string) => Array.from(row.querySelectorAll('button')).find(b => b.textContent!.trim() === text)!;
  const statusButton = (text: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('.claims-filter button')).find(b => b.textContent!.trim() === text)!;

  function claimsRequest(status: string): TestRequest {
    const req = http.expectOne(r => r.url === claimsUrl);
    expect(req.request.params.get('status')).toBe(status);
    return req;
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function loadPending(claims: GamertagClaimDto[] = [claim(1), claim(2, { kind: 'SetGamertag', gamertag: 'Old Timer', teamID: null, teamName: null, xboxGamertag: null, userID: null })]): Promise<void> {
    claimsRequest('Pending').flush(claims);
    await settle();
  }

  it('lists the pending claims, linking the user, placeholder, season and team', async () => {
    await loadPending();
    expect(Array.from(el().querySelectorAll('th')).every(th => th.getAttribute('scope') === 'col')).toBeTrue();
    expect(rows().length).toBe(2);
    const [first, second] = rows();
    expect(cells(first).slice(0, 6)).toEqual(['Legendary', 'Legendary117XUID 2535400000000001', 'User 501', 'Summer League 2010', 'Relentless', 'Roster match']);
    expect(Array.from(first.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/profile/117', '/profile/501', '/season/5', '/season/5/team/41']);
    expect(cells(second).slice(0, 6)).toEqual(['Old Timer', 'Unknown gamertag XUID 2535400000000002', 'User 501', 'Summer League 2010', '-', 'Set gamertag']);
    expect(Array.from(second.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/profile/501', '/season/5']);
    expect(button(first, 'Approve')).toBeDefined();
    expect(button(first, 'Reject')).toBeDefined();
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('tabindex')).toBe('0');
  });

  it('filters by status, offering decisions only on pending claims', async () => {
    await loadPending();
    expect(statusButton('Pending').getAttribute('aria-pressed')).toBe('true');

    statusButton('Approved').click();
    fixture.detectChanges();
    claimsRequest('Approved').flush([claim(3, { status: 'Approved' })]);
    await settle();
    expect(statusButton('Approved').getAttribute('aria-pressed')).toBe('true');
    expect(statusButton('Pending').getAttribute('aria-pressed')).toBe('false');
    expect(rows().length).toBe(1);
    expect(el().querySelector('tbody button')).toBeNull();
    expect(Array.from(el().querySelectorAll('th')).map(th => th.textContent!.trim())).not.toContain('Decision');

    statusButton('Rejected').click();
    fixture.detectChanges();
    claimsRequest('Rejected').flush([]);
    await settle();
    expect(el().querySelector('.hud-empty')!.textContent).toContain('No rejected claims.');
    expect(el().querySelector('table')).toBeNull();
  });

  it('approves a claim, says so and shows the list as it is now', async () => {
    await loadPending();
    button(rows()[0], 'Approve').click();
    fixture.detectChanges();
    const approve = http.expectOne('/api/GamertagClaims/ApproveClaim/1');
    expect(approve.request.method).toBe('POST');
    expect(button(rows()[1], 'Reject').disabled).withContext('one decision at a time').toBeTrue();

    approve.flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Approved the claim to Legendary.', 'Close');
    fixture.detectChanges();
    claimsRequest('Pending').flush([claim(2)]);
    await settle();
    expect(rows().length).toBe(1);
    expect(button(rows()[0], 'Approve').disabled).toBeFalse();
  });

  it('rejects a claim', async () => {
    await loadPending();
    button(rows()[1], 'Reject').click();
    http.expectOne('/api/GamertagClaims/RejectClaim/2').flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Rejected the claim to Old Timer.', 'Close');
    fixture.detectChanges();
    claimsRequest('Pending').flush([claim(1)]);
    await settle();
  });

  it("says why a decision was refused, reloading when someone else decided it first (409)", async () => {
    await loadPending();
    button(rows()[0], 'Approve').click();
    http.expectOne('/api/GamertagClaims/ApproveClaim/1')
      .flush({ title: 'Conflict', status: 409, detail: 'This claim was already rejected.' }, { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith('This claim was already rejected.', 'Close');
    fixture.detectChanges();
    claimsRequest('Pending').flush([claim(2)]);
    await settle();
  });

  it('says why a decision was refused (400), keeping the list', async () => {
    await loadPending();
    button(rows()[0], 'Reject').click();
    http.expectOne('/api/GamertagClaims/RejectClaim/1')
      .flush({ title: 'The placeholder already has an Xbox account.', status: 400 }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    expect(snackBar.open).toHaveBeenCalledWith('The placeholder already has an Xbox account.', 'Close');
    expect(rows().length).toBe(2);
    expect(button(rows()[0], 'Reject').disabled).toBeFalse();
  });

  it('says when the claims cannot be loaded', async () => {
    claimsRequest('Pending').flush('', { status: 500, statusText: 'down' });
    await settle();
    expect(el().querySelector('[role="alert"]')!.textContent).toContain("couldn't be loaded");
  });

  it('links back to user management', async () => {
    await loadPending([]);
    expect(el().querySelector('.hud-kicker')!.getAttribute('href')).toBe('/usermanagement');
    expect(el().querySelector('.hud-empty')!.textContent).toContain('No pending claims.');
  });
});

describe('problemText', () => {
  const error = (body: unknown, status = 400) => new HttpErrorResponse({ error: body, status });

  it("reads a ProblemDetails' detail, else its title", () => {
    expect(problemText(error({ title: 'Bad', detail: 'Too late' }), 'fallback')).toBe('Too late');
    expect(problemText(error({ title: 'Bad', detail: null }), 'fallback')).toBe('Bad');
    expect(problemText(error({}), 'fallback')).toBe('fallback');
  });

  it('reads a plain-text message, else falls back', () => {
    expect(problemText(error('Nope'), 'fallback')).toBe('Nope');
    expect(problemText(error(null, 0), 'fallback')).toBe('fallback');
  });
});
