import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { ClaimPreviewDto } from '@api/models';
import { ProfileIdentityStore } from './profileIdentity.store';

const claimsUrl = '/api/me/gamertag-claims';
const preview: ClaimPreviewDto = { gamertag: 'Old Me', placeholderName: 'Old Me', placeholderUserID: 501, refusal: null, seasons: [] };
const refused = [{ title: 'Refused', detail: 'No.' }, { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('ProfileIdentityStore', () => {
  let store: ProfileIdentityStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [ProfileIdentityStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(ProfileIdentityStore);
    http = TestBed.inject(HttpTestingController);
    TestBed.tick();
    http.expectOne(claimsUrl).flush([]);
    await TestBed.inject(ApplicationRef).whenStable();
  });

  afterEach(() => http.verify());

  it('loads the claims', () => {
    expect(store.claimsLoaded()).toBeTrue();
    expect(store.claimRows()).toEqual([]);
    expect(store.claimsFailed()).toBeFalse();
  });

  it('saves the region, or clears it, and says so', () => {
    const saved = jasmine.createSpy('saved');
    store.setRegion(2, saved);
    expect(store.savingRegion()).toBeTrue();
    const save = http.expectOne({ method: 'POST', url: '/api/me/region' });
    expect(save.request.body).toEqual({ regionID: 2 });
    save.flush(null);
    expect(store.savingRegion()).toBeFalse();
    expect(saved).toHaveBeenCalledTimes(1);
    expect(snackBar.open).toHaveBeenCalledWith('Region saved.', 'Close');

    store.setRegion(null, saved);
    http.expectOne('/api/me/region').flush(null);
    expect(snackBar.open).toHaveBeenCalledWith('Region cleared.', 'Close');
  });

  it('says why the region was not saved', () => {
    const saved = jasmine.createSpy('saved');
    store.setRegion(2, saved);
    http.expectOne('/api/me/region').flush(...refused);
    expect(store.savingRegion()).toBeFalse();
    expect(saved).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('No.', 'Close');
  });

  it('looks a gamertag up, and says why it could not', () => {
    store.previewClaim('Old Me');
    expect(store.previewing()).toBeTrue();
    http.expectOne(r => r.url === `${claimsUrl}/preview` && r.params.get('gamertag') === 'Old Me').flush(preview);
    expect(store.previewing()).toBeFalse();
    expect(store.preview()).toEqual(preview);

    store.previewClaim('Nobody');
    http.expectOne(r => r.url === `${claimsUrl}/preview`).flush(...refused);
    expect(store.previewing()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('No.', 'Close');
  });

  it('claims a gamertag, drops the preview, and loads the claims again', () => {
    store.previewClaim('Old Me');
    http.expectOne(r => r.url === `${claimsUrl}/preview`).flush(preview);
    const claimed = jasmine.createSpy('claimed');
    const body = { gamertag: 'Old Me', firstSeen: null, lastSeen: null };

    store.claim(body, claimed);
    expect(store.submitting()).toBeTrue();
    const send = http.expectOne({ method: 'POST', url: claimsUrl });
    expect(send.request.body).toEqual(body);
    send.flush({ gamertag: 'Old Me' });

    expect(store.submitting()).toBeFalse();
    expect(store.preview()).toBeNull();
    expect(claimed).toHaveBeenCalledTimes(1);
    expect(snackBar.open).toHaveBeenCalledWith('Your claim to Old Me is waiting for a sysadmin.', 'Close');
    TestBed.tick();
    http.expectOne(claimsUrl).flush([]);
  });

  it('says why a claim was refused, and keeps the preview', () => {
    store.previewClaim('Old Me');
    http.expectOne(r => r.url === `${claimsUrl}/preview`).flush(preview);
    const claimed = jasmine.createSpy('claimed');
    store.claim({ gamertag: 'Old Me', firstSeen: null, lastSeen: null }, claimed);
    http.expectOne({ method: 'POST', url: claimsUrl }).flush(...refused);
    expect(store.submitting()).toBeFalse();
    expect(store.preview()).toEqual(preview);
    expect(claimed).not.toHaveBeenCalled();
  });
});
