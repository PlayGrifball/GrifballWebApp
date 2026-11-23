import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ThemeComponent } from './theme.component';
import { ARCHIVE_VAULT, SPARTAN_HUD, ThemingService } from '../theming.service';
import { MatDialogRef } from '@angular/material/dialog';

describe('ThemeComponent', () => {
  let fixture: ComponentFixture<ThemeComponent>;
  let theming: ThemingService;

  beforeEach(async () => {
    spyOn(console, 'debug');
    await TestBed.configureTestingModule({ imports: [ThemeComponent] }).compileComponents();
    theming = TestBed.inject(ThemingService);
    theming.font.set('sans-serif');
    fixture = TestBed.createComponent(ThemeComponent);
    fixture.detectChanges();
  });

  it('has no Close or Done on the /theme page, outside a dialog', () => {
    const labels = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).map(b => b.getAttribute('aria-label') ?? b.textContent!.trim());
    expect(labels).not.toContain('Close');
    expect(labels).not.toContain('Done');
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

describe('ThemeComponent in the dialog', () => {
  let fixture: ComponentFixture<ThemeComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ThemeComponent>>;

  beforeEach(async () => {
    spyOn(console, 'debug');
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    await TestBed.configureTestingModule({
      imports: [ThemeComponent],
      providers: [{ provide: MatDialogRef, useValue: dialogRef }],
    }).compileComponents();
    fixture = TestBed.createComponent(ThemeComponent);
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('closes from the Close button in its header, which is labelled for screen readers', () => {
    const close = el().querySelector<HTMLButtonElement>('.theme-head button[aria-label="Close"]')!;
    expect(close).not.toBeNull();
    close.click();
    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('closes from Done at the bottom', () => {
    const done = Array.from(el().querySelectorAll<HTMLButtonElement>('.theme-foot button')).find(b => b.textContent!.trim() === 'Done')!;
    done.click();
    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });

  it('is titled for the dialog (ariaLabelledBy)', () => {
    expect(el().querySelector('#theme-title')!.textContent!.trim()).toBe('Theme');
  });
});
