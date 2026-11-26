import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { SpartanCatalog, SpartanChoice, SpartanProfileDto } from '@api/models';
import { ProfileSpartanStore } from './profileSpartan.store';

const stateUrl = '/api/me/spartan';
const profile: SpartanProfileDto = { choice: null, files: ['neutral-body'], hasEquippedSet: true, renderError: null, renderState: 'Done', renderedAt: '2026-11-20T10:00:00Z' };
const catalog: SpartanCatalog = { coatings: ['Ash'], cores: [{ name: 'Mark VII', regions: [] }], pendingCores: [] };
const choice: SpartanChoice = { core: 'Mark VII', armor: { helmet: 'Eod' }, parts: null, coating: null };
const refused = [{ title: 'Refused', detail: 'No.' }, { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('ProfileSpartanStore', () => {
  let store: ProfileSpartanStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [ProfileSpartanStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(ProfileSpartanStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts empty, then holds the loaded Spartan', () => {
    expect(store.profile()).toBeNull();
    expect(store.catalog()).toBeNull();
    expect(store.busy()).toBeFalse();
    store.load();
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile);
    expect(store.profile()).toEqual(profile);
  });

  it('looks again every half minute while a render is waiting, and stops when it is done', () => {
    jasmine.clock().install();
    try {
      store.load();
      http.expectOne(stateUrl).flush({ ...profile, renderState: 'Requested' });
      jasmine.clock().tick(29_000);
      http.expectNone(stateUrl);
      jasmine.clock().tick(1_000);
      http.expectOne(stateUrl).flush({ ...profile, renderState: 'Rendering' });
      jasmine.clock().tick(30_000);
      http.expectOne(stateUrl).flush(profile);
      jasmine.clock().tick(60_000);
      http.expectNone(stateUrl);
      expect(store.profile()).toEqual(profile);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says when the Spartan cannot be loaded', () => {
    store.load();
    http.expectOne(stateUrl).flush(...refused);
    expect(store.profile()).toBeNull();
    expect(snackBar.open).toHaveBeenCalledWith('No.', 'Close');
  });

  it('loads the catalog once', () => {
    store.loadCatalog();
    http.expectOne({ method: 'GET', url: '/api/spartan/catalog' }).flush(catalog);
    expect(store.catalog()).toEqual(catalog);
    store.loadCatalog();
    http.verify();
  });

  it('says when the catalog is not available, and asks again next time', () => {
    store.loadCatalog();
    http.expectOne('/api/spartan/catalog').flush('', { status: 503, statusText: 'down' });
    expect(store.catalog()).toBeNull();
    expect(snackBar.open).toHaveBeenCalledWith("The Spartan catalog isn't available right now.", 'Close');
    store.loadCatalog();
    http.expectOne('/api/spartan/catalog').flush(catalog);
    expect(store.catalog()).toEqual(catalog);
  });

  it('saves a choice, says so, and reloads the Spartan', () => {
    store.save(choice);
    expect(store.busy()).toBeTrue();
    const save = http.expectOne({ method: 'POST', url: stateUrl });
    expect(save.request.body).toEqual(choice);
    save.flush('', { status: 200, statusText: 'OK' });
    expect(store.busy()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('Spartan saved. It renders in a few minutes.', 'Close');
    http.expectOne({ method: 'GET', url: stateUrl }).flush({ ...profile, choice });
    expect(store.profile()!.choice).toEqual(choice);
  });

  it('says why a choice was not saved, and does not reload', () => {
    store.save(choice);
    http.expectOne({ method: 'POST', url: stateUrl }).flush(...refused);
    expect(store.busy()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('No.', 'Close');
    http.verify();
  });

  it('clears the choice so the equipped set shows again', () => {
    store.reset();
    expect(store.busy()).toBeTrue();
    http.expectOne({ method: 'POST', url: '/api/me/spartan/clear' }).flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Your equipped Spartan shows again.', 'Close');
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile);
    expect(store.busy()).toBeFalse();
  });

  it('says when the reset fails', () => {
    store.reset();
    http.expectOne('/api/me/spartan/clear').flush('', { status: 500, statusText: 'down' });
    expect(snackBar.open).toHaveBeenCalledWith("Couldn't reset your Spartan.", 'Close');
    expect(store.busy()).toBeFalse();
  });

  it('asks for a render and reloads the state', () => {
    store.render();
    http.expectOne({ method: 'POST', url: '/api/me/spartan/render' }).flush('', { status: 200, statusText: 'OK' });
    expect(snackBar.open).toHaveBeenCalledWith('Render asked for. It takes a few minutes.', 'Close');
    http.expectOne({ method: 'GET', url: stateUrl }).flush({ ...profile, renderState: 'Requested' });
    expect(store.profile()!.renderState).toBe('Requested');
  });

  it('says when a render cannot be asked for', () => {
    store.render();
    http.expectOne('/api/me/spartan/render').flush('', { status: 500, statusText: 'down' });
    expect(snackBar.open).toHaveBeenCalledWith("Couldn't ask for a render.", 'Close');
    expect(store.busy()).toBeFalse();
  });
});
