import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TileComponent } from './tile.component';
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

  const div = () => (fixture.nativeElement as HTMLElement).querySelector('div') as HTMLDivElement;

  it('shows the base colour with white text when no shade is given', () => {
    fixture.componentRef.setInput('i', null);
    fixture.detectChanges();

    expect(component.bg()).toBe('bg-primary');
    expect(component.textColor()).toBe('text-white');
    expect(div().className).toBe('bg-primary size-full text-white');
    expect(div().textContent).toContain('#f35700');
  });

  it('uses dark text on light shades (80 and above)', () => {
    fixture.componentRef.setInput('i', 90);
    fixture.detectChanges();

    expect(component.bg()).toBe('bg-primary-90');
    expect(component.textColor()).toBe('text-black');
    expect(div().textContent).toContain('90');
    expect(div().textContent).toContain('#ffdbcc');
  });

  it('uses white text on darker shades', () => {
    fixture.componentRef.setInput('i', 40);
    fixture.detectChanges();
    expect(component.textColor()).toBe('text-white');
    expect(component.hex()).toBe('#a33a00');
  });

  it('re-reads the hex value when a shade is applied', () => {
    fixture.componentRef.setInput('i', 40);
    fixture.detectChanges();
    html.style.setProperty('--primary-40', '#123456');
    TestBed.inject(ThemingService).shadeApplied.set({ name: 'primary-40', hex: '#123456' });
    fixture.detectChanges();

    expect(div().textContent).toContain('#123456');
  });
});
