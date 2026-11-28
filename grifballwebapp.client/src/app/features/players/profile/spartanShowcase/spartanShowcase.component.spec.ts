import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { SpartanProfileDto } from '@api/models';
import { SPARTAN_SCENE, SpartanScene } from '../spartanViewer/spartanScene';
import { SpartanShowcaseComponent } from './spartanShowcase.component';

function profile(changes: Partial<SpartanProfileDto> = {}): SpartanProfileDto {
  return { choice: null, files: [], hasEquippedSet: true, renderError: null, renderState: null, renderedAt: null, weapon: null, ...changes };
}

describe('SpartanShowcaseComponent', () => {
  let fixture: ComponentFixture<SpartanShowcaseComponent>;
  let shown: string[];
  let played: string[];

  beforeEach(() => {
    shown = [];
    played = [];
    const scene: SpartanScene = { show: async url => { shown.push(url); }, play: clip => { played.push(clip); }, dispose: () => undefined };
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
    await show(profile({ files: ['neutral-body', 'glb'], renderedAt: '2026-11-20T10:00:00Z' }));
    const images = Array.from(el().querySelectorAll('img'));
    expect(images).toHaveSize(1);
    expect(images[0].getAttribute('src')).toBe('/api/users/21/spartan/neutral-body?v=2026-11-20T10%3A00%3A00Z');
    expect(shown).toEqual(['/api/users/21/spartan/glb?v=2026-11-20T10%3A00%3A00Z']);
  });

  it('names the Spartan in the alt text and the 3D label', async () => {
    fixture.componentRef.setInput('name', 'IxGingy');
    await show(profile({ files: ['neutral-body', 'glb'] }));
    expect(el().querySelector('img')!.getAttribute('alt')).toBe('IxGingy, full body');
    expect(el().querySelector('canvas')!.getAttribute('aria-label')).toBe('IxGingy in 3D');
  });

  it('poses the one 3D model as neutral, and as the stance when asked', async () => {
    await show(profile({ files: ['glb', 'neutral-body', 'stance-body'], renderedAt: '2026-11-20T10:00:00Z' }));
    button('In stance')!.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(shown).toEqual(['/api/users/21/spartan/glb?v=2026-11-20T10%3A00%3A00Z']);
    expect(played).toEqual(['neutral', 'stance']);
    expect(button('In stance')!.getAttribute('aria-pressed')).toBe('true');
  });

  it('offers no pose choice when no stance was rendered', async () => {
    await show(profile({ files: ['glb', 'neutral-body'] }));
    expect(button('Neutral pose')).toBeUndefined();
    expect(shown).toEqual(['/api/users/21/spartan/glb']);
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
