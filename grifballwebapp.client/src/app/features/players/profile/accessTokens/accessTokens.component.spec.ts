import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccessTokensComponent } from './accessTokens.component';
import type { AccessTokenDto } from '@api/models';

const listUrl = '/api/identity/access-tokens';

function token(id: string, changes: Partial<AccessTokenDto> = {}): AccessTokenDto {
  return { id, name: 'Claude Code', createdAt: '2026-11-01T12:00:00Z', expiresAt: '2099-01-30T12:00:00Z', scope: 'Mcp', readOnly: false, ...changes };
}

describe('AccessTokensComponent', () => {
  let fixture: ComponentFixture<AccessTokensComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AccessTokensComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const button = (text: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === text)!;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function typeName(value: string): void {
    const input = el().querySelector<HTMLInputElement>('#at-name')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('lists your tokens with their dates, scope and read-only flag, and marks an expired one', async () => {
    http.expectOne(listUrl).flush([token('a', { scope: 'Both', readOnly: true }), token('b', { name: 'Old', expiresAt: '2020-01-01T00:00:00Z' })]);
    await settle();

    const rows = Array.from(el().querySelectorAll('.at-tokens li'));
    expect(rows).toHaveSize(2);
    expect(rows[0].textContent).toContain('Claude Code');
    expect(rows[0].textContent).toContain('Both');
    expect(rows[0].textContent).toContain('read-only');
    expect(rows[0].textContent).toContain('expires');
    expect(rows[1].textContent).not.toContain('read-only');
    expect(rows[1].querySelector('.at-expired')!.textContent).toContain('expired');
  });

  it('falls back to the raw value for a scope the page does not know', async () => {
    // Defensive: every real AccessTokenScope is in TOKEN_SCOPES, but a mismatch should still show something.
    http.expectOne(listUrl).flush([]);
    await settle();

    expect(fixture.componentInstance.scopeLabel('Unknown' as never)).toBe('Unknown');
  });

  it('says when there are none', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    expect(el().textContent).toContain('None yet.');
  });

  it('says when they cannot be loaded', async () => {
    http.expectOne(listUrl).flush('down', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(el().querySelector('[role="alert"]')!.textContent).toContain("couldn't be loaded");
  });

  it('makes a named token, shows it once with the Claude Code command, and reloads the list', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    expect(button('Make a token').disabled).toBeTrue();

    typeName('  Laptop  ');
    button('Make a token').click();
    const make = http.expectOne(req => req.method === 'POST' && req.url === listUrl);
    expect(make.request.body).toEqual({ name: 'Laptop', days: 90, scope: 'Mcp', readOnly: false });
    make.flush({ accessToken: token('c', { name: 'Laptop' }), token: 'grif_secret' });
    fixture.detectChanges();
    http.expectOne(listUrl).flush([token('c', { name: 'Laptop' })]);
    await settle();

    const shown = Array.from(el().querySelectorAll('.at-secret')).map(c => c.textContent);
    expect(shown[0]).toBe('grif_secret');
    expect(shown[1]).toContain('claude mcp add --transport http grifball');
    expect(shown[1]).toContain('/api/mcp --header "Authorization: Bearer grif_secret"');
    expect(el().querySelector<HTMLInputElement>('#at-name')!.value).toBe('');
  });

  it('makes a token for the lifetime, scope and read-only flag picked, and says why one was refused', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    const select = el().querySelector<HTMLSelectElement>('#at-days')!;
    select.value = '365';
    select.dispatchEvent(new Event('change'));
    const scopeSelect = el().querySelector<HTMLSelectElement>('#at-scope')!;
    scopeSelect.value = 'Api';
    scopeSelect.dispatchEvent(new Event('change'));
    el().querySelector<HTMLInputElement>('#at-readonly input')!.click();
    fixture.detectChanges();
    typeName('Desktop');

    button('Make a token').click();
    const make = http.expectOne(req => req.method === 'POST' && req.url === listUrl);
    expect(make.request.body).toEqual({ name: 'Desktop', days: 365, scope: 'Api', readOnly: true });
    make.flush({ title: 'Request refused', detail: 'You have 10 tokens already: revoke one first.' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith(jasmine.stringContaining('revoke one first'), 'Close');
    expect(el().querySelector('.at-created')).toBeNull();
  });

  it('leaves out the Claude Code command for a token scoped to the REST API only', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    const scopeSelect = el().querySelector<HTMLSelectElement>('#at-scope')!;
    scopeSelect.value = 'Api';
    scopeSelect.dispatchEvent(new Event('change'));
    typeName('Script');

    button('Make a token').click();
    const make = http.expectOne(req => req.method === 'POST' && req.url === listUrl);
    make.flush({ accessToken: token('c', { name: 'Script', scope: 'Api' }), token: 'grif_secret' });
    fixture.detectChanges();
    http.expectOne(listUrl).flush([token('c', { name: 'Script', scope: 'Api' })]);
    await settle();

    const shown = Array.from(el().querySelectorAll('.at-secret')).map(c => c.textContent);
    expect(shown).toEqual(['grif_secret']);
  });

  it('revokes a token and reloads the list', async () => {
    http.expectOne(listUrl).flush([token('a')]);
    await settle();

    button('Revoke').click();
    http.expectOne(req => req.method === 'POST' && req.url === `${listUrl}/a/revoke`).flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne(listUrl).flush([]);
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith('Revoked Claude Code.', 'Close');
    expect(el().textContent).toContain('None yet.');
  });

  it('makes one token at a time', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    typeName('Laptop');

    fixture.componentInstance.create();
    fixture.componentInstance.create();

    const made = http.match(req => req.method === 'POST' && req.url === listUrl);
    expect(made).toHaveSize(1);
    made[0].flush({ accessToken: token('c'), token: 'grif_secret' });
    fixture.detectChanges();
    http.expectOne(listUrl).flush([token('c')]);
  });

  it('says when a token cannot be revoked', async () => {
    http.expectOne(listUrl).flush([token('a')]);
    await settle();

    button('Revoke').click();
    http.expectOne(`${listUrl}/a/revoke`).flush({ title: 'Not found', detail: 'You have no such access token' },
      { status: 404, statusText: 'Not Found', headers: { 'Content-Type': 'application/problem+json' } });

    expect(snackBar.open).toHaveBeenCalledWith('You have no such access token', 'Close');
  });
});
