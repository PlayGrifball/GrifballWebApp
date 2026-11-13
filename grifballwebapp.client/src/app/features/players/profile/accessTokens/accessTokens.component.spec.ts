import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccessTokensComponent } from './accessTokens.component';
import type { AccessTokenDto } from '@api/models';

const listUrl = '/api/identity/access-tokens';

function token(id: string, changes: Partial<AccessTokenDto> = {}): AccessTokenDto {
  return { id, name: 'Claude Code', createdAt: '2026-11-01T12:00:00Z', expiresAt: '2099-01-30T12:00:00Z', ...changes };
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

  it('lists your tokens with their dates, and marks an expired one', async () => {
    http.expectOne(listUrl).flush([token('a'), token('b', { name: 'Old', expiresAt: '2020-01-01T00:00:00Z' })]);
    await settle();

    const rows = Array.from(el().querySelectorAll('.at-tokens li'));
    expect(rows).toHaveSize(2);
    expect(rows[0].textContent).toContain('Claude Code');
    expect(rows[0].textContent).toContain('expires');
    expect(rows[1].querySelector('.at-expired')!.textContent).toContain('expired');
  });

  it('says when there are none, and when they cannot be loaded', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    expect(el().textContent).toContain('None yet.');

    fixture.componentInstance.tokens.reload();
    await settle();
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
    expect(make.request.body).toEqual({ name: 'Laptop', days: 90 });
    make.flush({ accessToken: token('c', { name: 'Laptop' }), token: 'grif_secret' });
    await settle();
    http.expectOne(listUrl).flush([token('c', { name: 'Laptop' })]);
    await settle();

    const shown = Array.from(el().querySelectorAll('.at-secret')).map(c => c.textContent);
    expect(shown[0]).toBe('grif_secret');
    expect(shown[1]).toContain('claude mcp add --transport http grifball');
    expect(shown[1]).toContain('/api/mcp --header "Authorization: Bearer grif_secret"');
    expect(el().querySelector<HTMLInputElement>('#at-name')!.value).toBe('');
  });

  it('makes a token for the lifetime picked, and says why one was refused', async () => {
    http.expectOne(listUrl).flush([]);
    await settle();
    const select = el().querySelector<HTMLSelectElement>('#at-days')!;
    select.value = '365';
    select.dispatchEvent(new Event('change'));
    typeName('Desktop');

    button('Make a token').click();
    const make = http.expectOne(req => req.method === 'POST' && req.url === listUrl);
    expect(make.request.body).toEqual({ name: 'Desktop', days: 365 });
    make.flush({ title: 'Request refused', detail: 'You have 10 tokens already: revoke one first.' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith(jasmine.stringContaining('revoke one first'), 'Close');
    expect(el().querySelector('.at-created')).toBeNull();
  });

  it('revokes a token and reloads the list', async () => {
    http.expectOne(listUrl).flush([token('a')]);
    await settle();

    button('Revoke').click();
    http.expectOne(req => req.method === 'POST' && req.url === `${listUrl}/a/revoke`).flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    http.expectOne(listUrl).flush([]);
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith('Revoked Claude Code.', 'Close');
    expect(el().textContent).toContain('None yet.');
  });
});
