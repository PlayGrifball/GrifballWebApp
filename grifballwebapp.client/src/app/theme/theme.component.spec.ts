import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ThemeComponent } from './theme.component';
import { SPARTAN_HUD, ThemingService } from '../theming.service';

describe('ThemeComponent', () => {
  let fixture: ComponentFixture<ThemeComponent>;
  let theming: ThemingService;

  beforeEach(async () => {
    spyOn(console, 'log');
    await TestBed.configureTestingModule({ imports: [ThemeComponent] }).compileComponents();
    theming = TestBed.inject(ThemingService);
    theming.font.set('sans-serif');
    fixture = TestBed.createComponent(ThemeComponent);
    fixture.detectChanges();
  });

  it('starts with the current font selected', () => {
    expect(fixture.componentInstance.font.value).toBe('sans-serif');
    expect(fixture.componentInstance.fonts).toEqual(['Rajdhani', 'Roboto', 'sans-serif']);
  });

  it('updates the theme font when a new one is picked', () => {
    fixture.componentInstance.font.setValue('Roboto');
    expect(theming.font()).toBe('Roboto');
  });

  it('shows a palette editor for each palette', () => {
    const names = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('app-palette'))
      .map(p => p.getAttribute('palettename'));
    expect(names).toEqual(['primary', 'secondary', 'tertiary', 'neutral', 'neutral-variant', 'error']);
  });

  it('resets to Spartan HUD and rebuilds the palette editors', () => {
    theming.primary.set('#123456');
    const before = (fixture.nativeElement as HTMLElement).querySelector('app-palette');
    const reset = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent!.includes('Reset to Spartan HUD'))!;
    reset.click();
    fixture.detectChanges();

    expect(theming.primary()).toBe(SPARTAN_HUD.primary);
    expect(theming.font()).toBe('Rajdhani');
    expect(fixture.componentInstance.font.value).toBe('Rajdhani');
    expect((fixture.nativeElement as HTMLElement).querySelector('app-palette')).not.toBe(before);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('app-palette').length).toBe(6);
  });
});
