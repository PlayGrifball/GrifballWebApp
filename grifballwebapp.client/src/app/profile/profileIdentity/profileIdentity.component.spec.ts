import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProfileIdentityComponent } from './profileIdentity.component';
import { GamertagClaimDto } from '@api/models';

function claim(id: number, changes: Partial<GamertagClaimDto> = {}): GamertagClaimDto {
  return {
    gamertagClaimID: id, kind: 'UserClaim', status: 'Pending', xboxUserID: null, xboxGamertag: null, userID: 21, claimantUserID: 21,
    gamertag: 'Old Me', placeholderUserID: 501, seasonID: null, seasonName: null, teamID: null, teamName: null,
    createdAt: '2026-11-01T12:00:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [], ...changes,
  };
}

const claimsUrl = '/api/me/gamertag-claims';

describe('ProfileIdentityComponent', () => {
  let fixture: ComponentFixture<ProfileIdentityComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: MatSnackBar, useValue: snackBar }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileIdentityComponent);
    fixture.componentRef.setInput('regionID', 3);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const button = (text: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === text)!;
  const type = (selector: string, value: string) => {
    const input = el().querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows the region, labelled, and saves a new one', async () => {
    http.expectOne(claimsUrl).flush([]);
    await settle();
    const select = el().querySelector<HTMLSelectElement>('#pi-region')!;
    expect(el().querySelector('label[for="pi-region"]')).not.toBeNull();
    expect(select.selectedOptions[0].textContent!.trim()).toBe('East North America');
    let changed = 0;
    fixture.componentInstance.changed.subscribe(() => changed++);
    select.value = '2';
    select.dispatchEvent(new Event('change'));
    const save = http.expectOne('/api/me/region');
    expect(save.request.body).toEqual({ regionID: 2 });
    save.flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Region saved.', 'Close');
    expect(changed).toBe(1);
  });

  it('clears the region, and says when it cannot be saved', async () => {
    http.expectOne(claimsUrl).flush([]);
    await settle();
    const select = el().querySelector<HTMLSelectElement>('#pi-region')!;
    select.value = '';
    select.dispatchEvent(new Event('change'));
    const clear = http.expectOne('/api/me/region');
    expect(clear.request.body).toEqual({ regionID: null });
    clear.flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Region cleared.', 'Close');
    select.value = '1';
    select.dispatchEvent(new Event('change'));
    http.expectOne('/api/me/region').flush('There is no such region', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('There is no such region', 'Close');
  });

  it('previews a past gamertag, then sends the claim with its dates', async () => {
    http.expectOne(claimsUrl).flush([]);
    await settle();
    expect(el().textContent).toContain('None yet.');
    expect(button('Send claim').disabled).toBeTrue();
    type('#pi-gamertag', ' Old  Me ');
    type('#pi-from', '2018-06-01');
    button('Find its records').click();
    const preview = http.expectOne(r => r.url === '/api/me/gamertag-claims/preview');
    expect(preview.request.params.get('gamertag')).toBe('Old  Me');
    preview.flush({ gamertag: 'Old Me', placeholderUserID: 501, placeholderName: 'Old Me', seasons: [{ seasonID: 4, seasonName: 'Winter League 2019', year: 2019 }], refusal: null });
    fixture.detectChanges();
    expect(el().querySelector('[role="status"]')!.textContent).toContain('Old Me played Winter League 2019 (2019).');
    expect(button('Send claim').disabled).toBeFalse();
    button('Send claim').click();
    const send = http.expectOne('/api/me/gamertag-claims');
    expect(send.request.body).toEqual({ gamertag: 'Old  Me', firstSeen: '2018-06-01T00:00:00Z', lastSeen: null });
    send.flush(claim(9));
    expect(snackBar.open).toHaveBeenCalledWith('Your claim to Old Me is waiting for a sysadmin.', 'Close');
    fixture.detectChanges();
    http.expectOne(claimsUrl).flush([claim(9)]);
    await settle();
    expect(el().querySelector('.pi-claims')!.textContent).toContain('Old Me');
    expect(el().querySelector('.pi-claims')!.textContent).toContain('Pending');
    expect(el().querySelector('[role="status"]')).toBeNull();
  });

  it("shows why a gamertag can't be claimed, and a refused claim", async () => {
    http.expectOne(claimsUrl).flush([claim(4, { status: 'Rejected', rejectionReason: 'That was Bob' })]);
    await settle();
    expect(el().querySelector('.pi-claim-reason')!.textContent).toContain('That was Bob');
    type('#pi-gamertag', 'Taken');
    button('Find its records').click();
    http.expectOne(r => r.url === '/api/me/gamertag-claims/preview')
      .flush({ gamertag: 'Taken', placeholderUserID: null, placeholderName: null, seasons: [], refusal: "That gamertag belongs to another user's Xbox account." });
    fixture.detectChanges();
    expect(el().querySelector('.pi-refusal')!.textContent).toContain('belongs to another user');
    expect(button('Send claim').disabled).toBeTrue();
    fixture.componentInstance.submit();
    // Typed something else since the preview: not sendable either
    type('#pi-gamertag', 'Other');
    expect(button('Send claim').disabled).toBeTrue();
  });

  it('says when a preview or a claim fails, and when the claims cannot be loaded', async () => {
    http.expectOne(claimsUrl).flush('', { status: 500, statusText: 'down' });
    await settle();
    expect(el().querySelector('[role="alert"]')!.textContent).toContain("couldn't be loaded");
    fixture.componentInstance.previewClaim();
    type('#pi-gamertag', 'Old Me');
    button('Find its records').click();
    http.expectOne(r => r.url === '/api/me/gamertag-claims/preview').flush('', { status: 500, statusText: 'down' });
    expect(snackBar.open).toHaveBeenCalledWith("Couldn't look that gamertag up.", 'Close');
    button('Find its records').click();
    http.expectOne(r => r.url === '/api/me/gamertag-claims/preview').flush({ gamertag: 'Old Me', placeholderUserID: 501, placeholderName: null, seasons: [], refusal: null });
    fixture.detectChanges();
    expect(el().querySelector('[role="status"]')!.textContent).toContain('Old Me played no recorded seasons.');
    button('Send claim').click();
    http.expectOne('/api/me/gamertag-claims').flush({ title: 'Request refused', detail: 'You can make 5 claims a day; try again tomorrow' }, { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('You can make 5 claims a day; try again tomorrow', 'Close');
  });
});
