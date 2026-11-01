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
    teamName: 'Relentless', createdAt: '2026-10-28T15:00:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [], ...changes,
  };
}

const claimsUrl = '/api/GamertagClaims/GetClaims';
const xuidStatusUrl = '/api/GamertagClaims/XuidLookupStatus';
const noLookupsYet = { placeholders: 0, due: 0, found: 0, notFound: 0, failed: 0, lastLookedUpAt: null };

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
    // Loaded once on init, alongside the claims: not what most tests are about.
    http.expectOne(xuidStatusUrl).flush(noLookupsYet);
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
    expect(cells(first).slice(0, 6)).toEqual(['Legendary', 'Legendary117XUID 2535400000000001Gamertag history', 'User 501', 'Summer League 2010', 'Relentless', 'Roster match']);
    expect(Array.from(first.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/profile/117', '/usermanagement/pastgamertags?xboxUserID=2535400000000001', '/profile/501', '/season/5', '/season/5/team/41']);
    expect(cells(second).slice(0, 6)).toEqual(['Old Timer', 'Unknown gamertag XUID 2535400000000002Gamertag history', 'User 501', 'Summer League 2010', '-', 'Set gamertag']);
    expect(Array.from(second.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/usermanagement/pastgamertags?xboxUserID=2535400000000002', '/profile/501', '/season/5']);
    expect(button(first, 'Approve')).toBeDefined();
    expect(button(first, 'Reject')).toBeDefined();
    expect(el().querySelector('.hud-table-wrap')!.getAttribute('tabindex')).toBe('0');
  });

  it("shows who Xbox says currently holds a placeholder's gamertag, with the seasons it played in, to approve or reject", async () => {
    const lookup = claim(9, {
      kind: 'XuidLookup', xboxGamertag: null, userID: null, gamertag: 'Old Tag',
      currentGamertag: 'New Owner', lookedUpAt: '2026-10-29T10:00:00Z', placeholderSeasons: ['Winter League 2019 (2019)'],
    });
    await loadPending([lookup]);
    const [row] = rows();
    expect(cells(row)[0]).toContain('Old Tag');
    expect(cells(row)[0]).toContain('Winter League 2019 (2019)');
    expect(cells(row)[1]).toContain('Xbox says "Old Tag" is held now by New Owner (XUID 2535400000000009), looked up');
    expect(cells(row)[1]).toContain('The current holder may not be who played in Winter League 2019 (2019).');
    expect(cells(row)[5]).toBe('Xbox lookup');
    expect(button(row, 'Approve')).toBeDefined();
    expect(button(row, 'Reject')).toBeDefined();
  });

  it('says a lookup is still pending when it has none to show yet', async () => {
    const lookup = claim(9, { kind: 'XuidLookup', xboxGamertag: null, userID: null });
    await loadPending([lookup]);
    expect(cells(rows()[0])[1]).toContain('Not looked up yet.');
  });

  it('looks up XUIDs on demand and refreshes the status line', async () => {
    await loadPending([]);
    expect(el().querySelector('.claims-xuid')!.textContent).toContain('never');
    const lookUpButton = Array.from(el().querySelectorAll('button')).find(b => b.textContent!.trim() === 'Look up XUIDs')!;

    lookUpButton.click();
    fixture.detectChanges();
    expect(lookUpButton.disabled).toBeTrue();
    http.expectOne('/api/GamertagClaims/LookUpXuids').flush(
      { placeholders: 5, due: 5, found: 0, notFound: 0, failed: 0, lastLookedUpAt: null }, { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    http.expectOne(xuidStatusUrl).flush({ placeholders: 5, due: 2, found: 2, notFound: 1, failed: 0, lastLookedUpAt: '2026-10-29T10:00:00Z' });
    await settle();

    const statusText = el().querySelector('.claims-xuid')!.textContent!;
    expect(statusText).toContain('5 placeholders');
    expect(statusText).toContain('2 due');
    expect(statusText).toContain('2 found');
    expect(statusText).toContain('1 not found');
    expect(lookUpButton.disabled).toBeFalse();
  });

  it("says why a lookup couldn't start", async () => {
    await loadPending([]);
    const lookUpButton = Array.from(el().querySelectorAll('button')).find(b => b.textContent!.trim() === 'Look up XUIDs')!;
    lookUpButton.click();
    http.expectOne('/api/GamertagClaims/LookUpXuids').flush('busy', { status: 500, statusText: 'down' });
    // problemText returns the server's plain-text body ("busy") rather than the fallback here.
    expect(snackBar.open).toHaveBeenCalledWith('busy', 'Close');
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

describe('GamertagClaimsComponent: user claims and possible duplicates', () => {
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
    http.expectOne(xuidStatusUrl).flush(noLookupsYet);
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const buttonNamed = (root: Element, text: string) => Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === text)!;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const userClaim = claim(7, {
    kind: 'UserClaim', xboxUserID: null, xboxGamertag: null, userID: 30, claimantUserID: 30, claimantName: 'Spartan', gamertag: 'Old Me',
    seasonID: null, seasonName: null, teamID: null, teamName: null, firstSeen: '2018-06-01T00:00:00Z', lastSeen: null,
  });

  it("shows a user's claim with its claimant, and rejects it with the reason typed", async () => {
    http.expectOne(r => r.url === claimsUrl).flush([userClaim]);
    await settle();
    const row = el().querySelector('tbody tr')!;
    expect(row.textContent).toContain('Claimed by Spartan');
    expect(row.textContent).toContain('User claim');
    expect(row.textContent).toContain('Had it');
    expect(Array.from(row.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/profile/30', '/profile/501']);
    const reason = row.querySelector<HTMLInputElement>('input.claims-reason')!;
    expect(reason.getAttribute('aria-label')).toBe('Reason for rejecting the claim to Old Me');
    reason.value = '  Not you  ';
    reason.dispatchEvent(new Event('input'));
    buttonNamed(row, 'Reject').click();
    const reject = http.expectOne('/api/GamertagClaims/RejectClaim/7');
    expect(reject.request.body).toEqual({ reason: 'Not you' });
    reject.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne(r => r.url === claimsUrl).flush([]);
    await settle();
  });

  it('shows why a decided claim was rejected', async () => {
    http.expectOne(r => r.url === claimsUrl).flush([{ ...userClaim, status: 'Rejected', rejectionReason: 'That was Bob' }]);
    await settle();
    expect(el().querySelector('tbody tr')!.textContent).toContain('Rejected: That was Bob');
  });

  it('links to the merges page', async () => {
    http.expectOne(r => r.url === claimsUrl).flush([]);
    await settle();
    expect(el().querySelector('.claims-links a')!.getAttribute('href')).toBe('/usermanagement/merges');
  });

  const pair = {
    sourceUserID: 501, sourceGamertag: 'Shadow', targetUserID: 117, targetGamertag: 'Shadow#1234', targetIsPlaceholder: false,
    distance: 0, sourceSeasons: ['Winter League 2019 (2019)'], targetSeasons: [],
  };

  async function findDuplicates(body: object | string, status = 200): Promise<void> {
    http.expectOne(r => r.url === claimsUrl).flush([]);
    await settle();
    expect(el().querySelector('.claims-duplicates table')).toBeNull();
    buttonNamed(el(), 'Find possible duplicates').click();
    fixture.detectChanges();
    http.expectOne('/api/GamertagClaims/DuplicateSuggestions').flush(body, { status, statusText: status === 200 ? 'OK' : 'Error' });
    await settle();
  }

  it('finds possible duplicates on demand, and merges one', async () => {
    await findDuplicates([pair, { ...pair, sourceUserID: 502, sourceGamertag: 'Grunt Padree', targetUserID: 118, targetGamertag: 'Grunt Padre', targetIsPlaceholder: true, distance: 1, sourceSeasons: [] }]);
    const rows = Array.from(el().querySelectorAll('.claims-duplicates tbody tr'));
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Signed up · same gamertag');
    expect(rows[0].textContent).toContain('Winter League 2019 (2019)');
    expect(rows[1].textContent).toContain('Placeholder · 1 letter apart');
    buttonNamed(rows[0], 'Merge').click();
    fixture.detectChanges();
    expect(buttonNamed(rows[1], 'Dismiss').disabled).toBeTrue();
    const merge = http.expectOne('/api/GamertagClaims/MergeDuplicate');
    expect(merge.request.body).toEqual({ sourceUserID: 501, targetUserID: 117 });
    merge.flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Merged Shadow into Shadow#1234.', 'Close');
    fixture.detectChanges();
    http.expectOne('/api/GamertagClaims/DuplicateSuggestions').flush([]);
    await settle();
    expect(el().querySelector('.claims-duplicates .hud-empty')!.textContent).toContain('No possible duplicates.');
  });

  it('dismisses a pair, says why a merge was refused, and finds again', async () => {
    await findDuplicates([{ ...pair, distance: 2 }]);
    const row = el().querySelector('.claims-duplicates tbody tr')!;
    expect(row.textContent).toContain('2 letters apart');
    buttonNamed(row, 'Merge').click();
    http.expectOne('/api/GamertagClaims/MergeDuplicate').flush({ title: 'Conflict', detail: 'Both users are on different teams.' }, { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith('Both users are on different teams.', 'Close');
    fixture.detectChanges();
    buttonNamed(row, 'Dismiss').click();
    http.expectOne('/api/GamertagClaims/DismissDuplicate').flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Dismissed Shadow and Shadow#1234.', 'Close');
    fixture.detectChanges();
    http.expectOne('/api/GamertagClaims/DuplicateSuggestions').flush([pair]);
    await settle();
    buttonNamed(el(), 'Find possible duplicates').click();
    fixture.detectChanges();
    http.expectOne('/api/GamertagClaims/DuplicateSuggestions').flush([]);
    await settle();
  });

  it('says when the suggestions cannot be loaded', async () => {
    await findDuplicates('', 500);
    expect(el().querySelector('.claims-duplicates [role="alert"]')!.textContent).toContain("couldn't be loaded");
  });
});
