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
  let clips: string[];

  beforeEach(() => {
    shown = [];
    played = [];
    clips = ['neutral', 'stance'];
    const scene: SpartanScene = { show: async url => { shown.push(url); return clips; }, play: clip => { played.push(clip); }, dispose: () => undefined };
    TestBed.configureTestingModule({ providers: [{ provide: SPARTAN_SCENE, useValue: async () => scene }] });
    fixture = TestBed.createComponent(SpartanShowcaseComponent);
    fixture.componentRef.setInput('userID', 21);
  });

  const el = (): HTMLElement => fixture.nativeElement;
  const select = (): HTMLSelectElement | null => el().querySelector('select');
  const options = (): string[] => Array.from(select()?.options ?? []).map(o => o.textContent!.trim());
  async function choose(label: string): Promise<void> {
    const s = select()!;
    s.value = Array.from(s.options).find(o => o.textContent!.trim() === label)!.value;
    s.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  async function show(p: SpartanProfileDto | null): Promise<void> {
    fixture.componentRef.setInput('profile', p);
    for (let i = 0; i < 3; i++) {
      fixture.detectChanges();
      await fixture.whenStable();
      await new Promise<void>(resolve => setTimeout(resolve));
    }
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
    await choose('In stance');

    expect(shown).toEqual(['/api/users/21/spartan/glb?v=2026-11-20T10%3A00%3A00Z']);
    expect(played).toEqual(['neutral', 'stance']);
    expect(select()!.value).toBe('stance');
    expect(el().querySelector('label[for="spartan-pose-21"]')!.textContent!.trim()).toBe('Pose');
  });

  it('offers every other pose the model carries, by its name, and poses the model in it', async () => {
    clips = ['neutral', 'stance', 'Ruthless War Club', "Captain's Pride"];
    await show(profile({ files: ['glb', 'neutral-body', 'stance-body'] }));

    expect(options()).toEqual(['Neutral pose', 'In stance', 'Ruthless War Club', "Captain's Pride"]);
    await choose("Captain's Pride");
    expect(played.at(-1)).toBe("Captain's Pride");
  });

  it('leaves out the stance when none was rendered, but still offers the other poses', async () => {
    clips = ['neutral', 'stance', 'Ruthless War Club'];
    await show(profile({ files: ['glb', 'neutral-body'] }));

    expect(options()).toEqual(['Neutral pose', 'Ruthless War Club']);
  });

  it('offers no pose choice when the model has only the one pose', async () => {
    await show(profile({ files: ['glb', 'neutral-body'] }));
    expect(select()).toBeNull();
    expect(shown).toEqual(['/api/users/21/spartan/glb']);
  });

  it('goes back to neutral when a new model lacks the pose it was in', async () => {
    clips = ['neutral', 'stance', 'Ruthless War Club'];
    await show(profile({ files: ['glb', 'neutral-body', 'stance-body'], renderedAt: '2026-11-20T10:00:00Z' }));
    await choose('Ruthless War Club');

    clips = ['neutral', 'stance'];
    await show(profile({ files: ['glb', 'neutral-body', 'stance-body'], renderedAt: '2026-11-21T10:00:00Z' }));

    expect(select()!.value).toBe('neutral');
    expect(played.at(-1)).toBe('neutral');
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
