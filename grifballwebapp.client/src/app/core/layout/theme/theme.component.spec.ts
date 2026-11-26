import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ThemeComponent } from './theme.component';
import { ARCHIVE_VAULT, SPARTAN_HUD, ThemingService } from '../theming.service';
import { ThemePersistence } from '../themePersistence.service';

describe('ThemeComponent', () => {
  let fixture: ComponentFixture<ThemeComponent>;
  let theming: ThemingService;

  beforeEach(async () => {
    spyOn(console, 'debug');
    await TestBed.configureTestingModule({
      imports: [ThemeComponent],
      providers: [{ provide: ThemePersistence, useValue: { restored: signal(0) } }],
    }).compileComponents();
    theming = TestBed.inject(ThemingService);
    theming.font.set('sans-serif');
    fixture = TestBed.createComponent(ThemeComponent);
    fixture.detectChanges();
  });

  it('is titled Theme, for the profile section that names it', () => {
    expect((fixture.nativeElement as HTMLElement).querySelector('#theme-title')!.textContent!.trim()).toBe('Theme');
  });

  it('follows the font when the theme is replaced, as a saved one arriving does', () => {
    theming.applySettings({ ...theming.settings(), font: 'Roboto' });
    fixture.detectChanges();
    expect(fixture.componentInstance.font.value).toBe('Roboto');
  });

  it('rebuilds the palette editors when the account\'s saved theme arrives', () => {
    const before = (fixture.nativeElement as HTMLElement).querySelector('app-palette');
    TestBed.inject(ThemePersistence).restored.update(n => n + 1);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-palette')).not.toBe(before);
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

  it('resets to Archive Vault and rebuilds the palette editors', () => {
    theming.primary.set('#123456');
    const before = (fixture.nativeElement as HTMLElement).querySelector('app-palette');
    const reset = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent!.includes('Reset to Archive Vault'))!;
    reset.click();
    fixture.detectChanges();

    expect(theming.primary()).toBe(ARCHIVE_VAULT.primary);
    expect(theming.era()).toBe(ARCHIVE_VAULT.era);
    expect(theming.font()).toBe('Rajdhani');
    expect(fixture.componentInstance.font.value).toBe('Rajdhani');
    expect((fixture.nativeElement as HTMLElement).querySelector('app-palette')).not.toBe(before);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('app-palette')).toHaveSize(6);
  });

  it('ignores a preset that does not exist', () => {
    const before = theming.primary();
    fixture.componentInstance.selectTheme(999);
    expect(theming.primary()).toBe(before);
  });

  it('applies an era or legacy preset when clicked', () => {
    const btn = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(b => b.textContent!.trim() === SPARTAN_HUD.name)!;
    btn.click();
    fixture.detectChanges();

    expect(theming.primary()).toBe(SPARTAN_HUD.primary);
    expect(theming.era()).toBe(SPARTAN_HUD.era);
  });
});
