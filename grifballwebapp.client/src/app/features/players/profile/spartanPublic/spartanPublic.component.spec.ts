import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { SpartanProfileDto } from '@api/models';
import { SPARTAN_SCENE, SpartanScene } from '../spartanViewer/spartanScene';
import { SpartanPublicComponent } from './spartanPublic.component';

function profile(changes: Partial<SpartanProfileDto> = {}): SpartanProfileDto {
  return { choice: null, files: [], hasEquippedSet: true, renderError: null, renderState: null, renderedAt: null, weapon: null, ...changes };
}

describe('SpartanPublicComponent', () => {
  let fixture: ComponentFixture<SpartanPublicComponent>;
  let http: HttpTestingController;
  let shown: string[];

  beforeEach(() => {
    shown = [];
    const scene: SpartanScene = { show: async url => { shown.push(url); }, play: () => undefined, dispose: () => undefined };
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: SPARTAN_SCENE, useValue: async () => scene }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SpartanPublicComponent);
    fixture.componentRef.setInput('userID', 1014);
    fixture.componentRef.setInput('name', 'IxGingy');
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  async function answer(body: SpartanProfileDto): Promise<void> {
    http.expectOne({ method: 'GET', url: '/api/users/1014/spartan' }).flush(body);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows another user\'s Spartan in 3D, without signing in as them', async () => {
    await answer(profile({ files: ['neutral-body', 'glb'] }));
    expect((fixture.nativeElement as HTMLElement).querySelector('img')!.getAttribute('src')).toBe('/api/users/1014/spartan/neutral-body');
    expect(shown).toEqual(['/api/users/1014/spartan/glb']);
  });

  it('shows nothing for a user who has not rendered', async () => {
    await answer(profile());
    expect((fixture.nativeElement as HTMLElement).querySelector('section')).toBeNull();
  });

  it('shows nothing when the Spartan cannot be loaded', async () => {
    http.expectOne('/api/users/1014/spartan').flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('section')).toBeNull();
  });
});
