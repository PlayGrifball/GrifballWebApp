import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { SpartanProfileDto } from '@api/models';
import { SPARTAN_SCENE, SpartanScene } from '../spartanViewer/spartanScene';
import { SpartanShowcaseComponent } from './spartanShowcase.component';

function profile(changes: Partial<SpartanProfileDto> = {}): SpartanProfileDto {
  return { choice: null, files: [], hasEquippedSet: true, renderError: null, renderState: null, renderedAt: null, ...changes };
}

describe('SpartanShowcaseComponent', () => {
  let fixture: ComponentFixture<SpartanShowcaseComponent>;
  let shown: string[];

  beforeEach(() => {
    shown = [];
    const scene: SpartanScene = { show: async url => { shown.push(url); }, dispose: () => undefined };
    TestBed.configureTestingModule({ providers: [{ provide: SPARTAN_SCENE, useValue: async () => scene }] });
    fixture = TestBed.createComponent(SpartanShowcaseComponent);
    fixture.componentRef.setInput('userID', 21);
  });

  const el = (): HTMLElement => fixture.nativeElement;
  const button = (label: string): HTMLButtonElement | undefined =>
    Array.from(el().querySelectorAll('button')).find(b => b.textContent?.trim() === label);

  async function show(p: SpartanProfileDto | null): Promise<void> {
    fixture.componentRef.setInput('profile', p);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows only the files that exist, from the user\'s own URLs', async () => {
    await show(profile({ files: ['neutral-body', 'neutral-glb', 'stance-glb'], renderedAt: '2026-11-20T10:00:00Z' }));
    const images = Array.from(el().querySelectorAll('img'));
    expect(images).toHaveSize(1);
    expect(images[0].getAttribute('src')).toBe('/api/users/21/spartan/neutral-body?v=2026-11-20T10%3A00%3A00Z');
    expect(shown).toEqual(['/api/users/21/spartan/neutral-glb?v=2026-11-20T10%3A00%3A00Z']);
  });

  it('names the Spartan in the alt text and the 3D label', async () => {
    fixture.componentRef.setInput('name', 'IxGingy');
    await show(profile({ files: ['neutral-body', 'neutral-glb'] }));
    expect(el().querySelector('img')!.getAttribute('alt')).toBe('IxGingy, full body');
    expect(el().querySelector('canvas')!.getAttribute('aria-label')).toBe('IxGingy in 3D');
  });

  it('switches the 3D model between the neutral pose and the stance', async () => {
    await show(profile({ files: ['neutral-glb', 'stance-glb'], renderedAt: '2026-11-20T10:00:00Z' }));
    button('In stance')!.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(shown).toEqual(['/api/users/21/spartan/neutral-glb?v=2026-11-20T10%3A00%3A00Z', '/api/users/21/spartan/stance-glb?v=2026-11-20T10%3A00%3A00Z']);
    expect(button('In stance')!.getAttribute('aria-pressed')).toBe('true');
  });

  it('offers no pose choice, and shows the model there is, when only one exists', async () => {
    await show(profile({ files: ['stance-glb'] }));
    expect(button('Neutral pose')).toBeUndefined();
    expect(shown).toEqual(['/api/users/21/spartan/stance-glb']);
  });

  it('leaves the version out of the addresses while nothing has rendered', async () => {
    await show(profile({ files: ['neutral-body'] }));
    expect(el().querySelector('img')!.getAttribute('src')).toBe('/api/users/21/spartan/neutral-body');
  });

  it('shows nothing when nothing has rendered, or the profile has not loaded', async () => {
    await show(profile());
    expect(el().querySelector('img')).toBeNull();
    expect(el().querySelector('app-spartan-viewer')).toBeNull();
    await show(null);
    expect(el().textContent?.trim()).toBe('');
  });
});
