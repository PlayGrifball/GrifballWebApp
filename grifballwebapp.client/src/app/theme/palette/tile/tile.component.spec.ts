import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TileComponent, readableOn } from './tile.component';
import { ThemingService } from '../../../theming.service';

describe('TileComponent', () => {
  let fixture: ComponentFixture<TileComponent>;
  let component: TileComponent;
  const html = document.querySelector('html')!;

  beforeEach(async () => {
    html.style.setProperty('--primary', '#f35700');
    html.style.setProperty('--primary-90', '#ffdbcc');
    html.style.setProperty('--primary-40', '#a33a00');
    await TestBed.configureTestingModule({ imports: [TileComponent] }).compileComponents();
    fixture = TestBed.createComponent(TileComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('paletteName', 'primary');
  });

  afterEach(() => {
    html.style.removeProperty('--primary');
    html.style.removeProperty('--primary-90');
    html.style.removeProperty('--primary-40');
  });

  const tile = () => (fixture.nativeElement as HTMLElement).querySelector('.tile') as HTMLDivElement;
  const lines = () => Array.from(tile().children).map(c => c.textContent!.trim());

  it('shows the seed colour, labelled, with its hex on its own line', () => {
    fixture.componentRef.setInput('i', null);
    fixture.detectChanges();

    expect(component.bg()).toBe('bg-primary');
    expect(tile().classList).toContain('bg-primary');
    expect(lines()).toEqual(['seed', '#f35700']);
  });

  it('shows a tone over its hex, on separate lines', () => {
    fixture.componentRef.setInput('i', 90);
    fixture.detectChanges();

    expect(component.bg()).toBe('bg-primary-90');
    expect(tile().classList).toContain('bg-primary-90');
    expect(lines()).toEqual(['90', '#ffdbcc']);
  });

  it('uses dark text on a light tone and white on a dark one', () => {
    fixture.componentRef.setInput('i', 90);
    fixture.detectChanges();
    expect(component.textColor()).toBe('#000000');
    expect(tile().style.color).toBe('rgb(0, 0, 0)');

    fixture.componentRef.setInput('i', 40);
    fixture.detectChanges();
    expect(component.textColor()).toBe('#ffffff');
    expect(component.hex()).toBe('#a33a00');
  });

  it('re-reads the hex value when a shade is applied', () => {
    fixture.componentRef.setInput('i', 40);
    fixture.detectChanges();
    html.style.setProperty('--primary-40', '#123456');
    TestBed.inject(ThemingService).shadeApplied.set({ name: 'primary-40', hex: '#123456' });
    fixture.detectChanges();

    expect(tile().textContent).toContain('#123456');
  });

  it('picks the readable text colour by luminance', () => {
    expect(readableOn('#ffffff')).toBe('#000000');
    expect(readableOn('#7CCBFF')).toBe('#000000'); // the light Spartan HUD blue
    expect(readableOn('#808080')).toBe('#000000');
    expect(readableOn('#2A5373')).toBe('#ffffff');
    expect(readableOn('#000000')).toBe('#ffffff');
    expect(readableOn('ffdbcc')).toBe('#000000');
    expect(readableOn(undefined)).toBe('#ffffff');
    expect(readableOn('red')).toBe('#ffffff');
  });
});
