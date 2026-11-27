import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { SpartanCatalog, SpartanProfileDto } from '@api/models';
import { SPARTAN_SCENE, SpartanScene } from '../spartanViewer/spartanScene';
import { ProfileSpartanComponent } from './profileSpartan.component';

const stateUrl = '/api/me/spartan';
const catalog: SpartanCatalog = {
  coatings: ['Ash'],
  visors: ['Mirror'],
  pendingCores: [],
  cores: [
    { name: 'Mark VII', regions: [
      { key: 'helmet', kind: 'REGION', region: 1, pieces: [{ name: 'Eod', permutation: 1 }, { name: null, permutation: 2 }] },
      { key: 'wheel', kind: 'OTHER', region: 2, pieces: [{ name: 'Round', permutation: 1 }] },
      { key: 'unnamed', kind: 'REGION', region: 3, pieces: [{ name: null, permutation: 1 }] },
    ] },
    { name: 'Mark V', regions: [] },
  ],
};

function profile(changes: Partial<SpartanProfileDto> = {}): SpartanProfileDto {
  return { choice: null, files: [], hasEquippedSet: true, renderError: null, renderState: null, renderedAt: null, ...changes };
}

describe('ProfileSpartanComponent', () => {
  let fixture: ComponentFixture<ProfileSpartanComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let shown: string[];

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    shown = [];
    const scene: SpartanScene = { show: async url => { shown.push(url); }, play: () => undefined, dispose: () => undefined };
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }, { provide: SPARTAN_SCENE, useValue: async () => scene }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileSpartanComponent);
    fixture.componentRef.setInput('userID', 21);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');
  const button = (label: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === label);
  const pick = (selector: string, value: string) => {
    const select = el().querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  async function load(p: SpartanProfileDto): Promise<void> {
    http.expectOne({ method: 'GET', url: stateUrl }).flush(p);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function openForm(p: SpartanProfileDto): Promise<void> {
    await load(p);
    button('Choose a Spartan')!.click();
    http.expectOne('/api/spartan/catalog').flush(catalog);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows nothing of the state until it loads', () => {
    expect(el().querySelector('.spartan-actions')).toBeNull();
    http.expectOne(stateUrl);
  });

  it('says the equipped set shows when there is no choice', async () => {
    await load(profile());
    expect(text()).toContain('Showing the set you have equipped in Halo Infinite.');
    expect(button('Use my equipped set')).toBeUndefined();
  });

  it('says the chosen Spartan shows, and offers going back to the equipped set', async () => {
    await load(profile({ choice: { core: 'Mark VII', armor: null, parts: null, coating: null, visor: null, disableDamage: false } }));
    expect(text()).toContain('Showing the Spartan you chose.');
    button('Use my equipped set')!.click();
    http.expectOne({ method: 'POST', url: '/api/me/spartan/clear' }).flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile());
  });

  it('asks the user to choose when no equipped set is known', async () => {
    await load(profile({ hasEquippedSet: false }));
    expect(text()).toContain('No equipped set is known for your account yet');
  });

  it('shows the render state', async () => {
    await load(profile({ renderState: 'Requested' }));
    expect(text()).toContain('It is waiting to render.');
    fixture.componentInstance.render();
    http.expectOne('/api/me/spartan/render').flush('', { status: 200, statusText: 'OK' });
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile({ renderState: 'Rendering' }));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(text()).toContain('It is rendering.');
  });

  it('shows a failed render with its error, and a done one with its date', async () => {
    await load(profile({ renderState: 'Failed', renderError: 'Out of memory' }));
    expect(text()).toContain('The last render failed: Out of memory');
    button('Render again')!.click();
    http.expectOne('/api/me/spartan/render').flush('', { status: 200, statusText: 'OK' });
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile({ renderState: 'Done', renderedAt: '2026-11-20T10:00:00Z' }));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(text()).toContain('Rendered Nov 20, 2026');
  });

  it('shows its render in the showcase, from the user\'s own URLs', async () => {
    await load(profile({ files: ['neutral-body', 'glb'], renderedAt: '2026-11-20T10:00:00Z' }));
    expect(el().querySelector('img')!.getAttribute('src')).toBe('/api/users/21/spartan/neutral-body?v=2026-11-20T10%3A00%3A00Z');
    await fixture.whenStable();
    expect(shown).toEqual(['/api/users/21/spartan/glb?v=2026-11-20T10%3A00%3A00Z']);
  });

  it('opens the chooser with what is chosen now, after loading the catalog', async () => {
    await load(profile({ choice: { core: 'Mark VII', armor: { helmet: 'Eod' }, parts: null, coating: 'Ash', visor: 'Mirror', disableDamage: true } }));
    button('Choose a Spartan')!.click();
    fixture.detectChanges();
    expect(text()).toContain('Loading what you can choose from');
    http.expectOne('/api/spartan/catalog').flush(catalog);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el().querySelector<HTMLSelectElement>('#spartan-core')!.value).toBe('Mark VII');
    expect(el().querySelector<HTMLSelectElement>('#spartan-helmet')!.value).toBe('Eod');
    expect(el().querySelector<HTMLSelectElement>('#spartan-coating')!.value).toBe('Ash');
    expect(el().querySelector<HTMLSelectElement>('#spartan-visor')!.value).toBe('Mirror');
    expect(el().querySelector<HTMLInputElement>('#spartan-disable-damage')!.checked).toBeTrue();
  });

  it('keeps Save off until a core is picked, and offers only named pieces of regions', async () => {
    await openForm(profile());
    expect(button('Save')!.disabled).toBeTrue();
    expect(el().querySelector('#spartan-helmet')).toBeNull();
    pick('#spartan-core', 'Mark VII');
    expect(button('Save')!.disabled).toBeFalse();
    const options = Array.from(el().querySelectorAll<HTMLOptionElement>('#spartan-helmet option')).map(o => o.textContent!.trim());
    expect(options).toEqual(['Default', 'Eod']);
    expect(el().querySelector('#spartan-wheel')).toBeNull();
    expect(el().querySelector('#spartan-unnamed')).toBeNull();
  });

  it('saves the picked core, piece, coating and visor, and closes the form', async () => {
    await openForm(profile());
    pick('#spartan-core', 'Mark VII');
    pick('#spartan-helmet', 'Eod');
    pick('#spartan-coating', 'Ash');
    pick('#spartan-visor', 'Mirror');
    const clean = el().querySelector<HTMLInputElement>('#spartan-disable-damage')!;
    clean.click();
    button('Save')!.click();
    fixture.detectChanges();
    const save = http.expectOne({ method: 'POST', url: stateUrl });
    expect(save.request.body).toEqual({ core: 'Mark VII', armor: { helmet: 'Eod' }, parts: null, coating: 'Ash', visor: 'Mirror', disableDamage: false });
    save.flush('', { status: 200, statusText: 'OK' });
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile());
    expect(el().querySelector('form')).toBeNull();
  });

  it('drops the picked pieces when the core changes, and an unpicked piece is left out', async () => {
    await openForm(profile());
    pick('#spartan-core', 'Mark VII');
    pick('#spartan-helmet', 'Eod');
    pick('#spartan-helmet', '');
    expect(Object.values(fixture.componentInstance.form.getRawValue().armor).filter(Boolean)).toEqual([]);
    pick('#spartan-helmet', 'Eod');
    pick('#spartan-core', 'Mark V');
    expect(Object.values(fixture.componentInstance.form.getRawValue().armor).filter(Boolean)).toEqual([]);
  });

  it('keeps the saved parts only while the core is the same', async () => {
    const parts = [{ region: 'helmet', permutation: 3 }] as never;
    await openForm(profile({ choice: { core: 'Mark VII', armor: null, parts, coating: null, visor: null, disableDamage: false } }));
    button('Save')!.click();
    const same = http.expectOne({ method: 'POST', url: stateUrl });
    expect(same.request.body.parts).toEqual(parts);
    same.flush('', { status: 200, statusText: 'OK' });
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile());

    fixture.componentInstance.edit();
    fixture.componentInstance.form.controls.core.setValue('Mark V');
    fixture.componentInstance.save();
    expect(http.expectOne({ method: 'POST', url: stateUrl }).request.body.parts).toBeNull();
  });

  it('closes the form on Cancel without saving', async () => {
    await openForm(profile());
    button('Cancel')!.click();
    fixture.detectChanges();
    expect(el().querySelector('form')).toBeNull();
  });

  it('clears the choice from the component and closes the form', async () => {
    await openForm(profile({ choice: { core: 'Mark VII', armor: null, parts: null, coating: null, visor: null, disableDamage: false } }));
    fixture.componentInstance.reset();
    fixture.detectChanges();
    expect(el().querySelector('form')).toBeNull();
    http.expectOne({ method: 'POST', url: '/api/me/spartan/clear' }).flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne({ method: 'GET', url: stateUrl }).flush(profile());
  });

  it('disables the buttons while a request is running', async () => {
    await load(profile());
    fixture.componentInstance.render();
    fixture.detectChanges();
    expect(button('Choose a Spartan')!.disabled).toBeTrue();
    expect(button('Render again')!.disabled).toBeTrue();
    http.expectOne('/api/me/spartan/render').flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(button('Render again')!.disabled).toBeFalse();
  });
});
