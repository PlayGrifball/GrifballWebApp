import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaletteComponent } from './palette.component';
import { ThemingService } from '../../theming.service';
import { palette } from '../paletteTypes';

describe('PaletteComponent', () => {
  const html = document.querySelector('html')!;
  let theming: ThemingService;

  beforeEach(async () => {
    html.style.setProperty('--secondary', '#8e0003');
    await TestBed.configureTestingModule({ imports: [PaletteComponent] }).compileComponents();
    theming = TestBed.inject(ThemingService);
    spyOn(console, 'log');
  });

  afterEach(() => html.style.removeProperty('--secondary'));

  function create(name: palette): ComponentFixture<PaletteComponent> {
    const fixture = TestBed.createComponent(PaletteComponent);
    fixture.componentRef.setInput('paletteName', name);
    fixture.detectChanges();
    return fixture;
  }

  it('starts the picker at the current CSS colour of the palette', () => {
    const fixture = create('secondary');
    expect(fixture.componentInstance.color.value).toBe('#8e0003');
  });

  it('renders a base tile plus one tile per shade from 100 down to 0', () => {
    const fixture = create('secondary');
    expect(fixture.componentInstance.range()).toEqual([100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 0]);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('app-tile').length).toBe(12);
  });

  const signals: [palette, keyof ThemingService][] = [
    ['primary', 'primary'],
    ['secondary', 'secondary'],
    ['tertiary', 'tertiary'],
    ['neutral', 'neutral'],
    ['neutral-variant', 'neutralVariant'],
    ['error', 'error'],
  ];

  for (const [name, key] of signals) {
    it(`writes picked colours to the ${String(key)} theme signal`, () => {
      const fixture = create(name);
      fixture.componentInstance.color.setValue('#abcdef');
      expect((theming[key] as () => string)()).toBe('#abcdef');
    });
  }

  it('ignores colours for an unknown palette name', () => {
    const before = theming.primary();
    const fixture = create('unknown' as palette);
    fixture.componentInstance.color.setValue('#abcdef');
    expect(theming.primary()).toBe(before);
  });
});
