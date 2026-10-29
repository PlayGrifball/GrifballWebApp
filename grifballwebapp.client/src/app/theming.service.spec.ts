import { TestBed } from '@angular/core/testing';
import { SPARTAN_HUD, ThemingService } from './theming.service';

describe('ThemingService', () => {
  let service: ThemingService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ThemingService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have default color values', () => {
    // Spartan HUD
    expect(service.primary()).toBe('#7CCBFF');
    expect(service.secondary()).toBe('#2A5373');
    expect(service.tertiary()).toBe('#FFD25E');
    expect(service.neutral()).toBe('#5F7F96');
    expect(service.neutralVariant()).toBe('#4F7391');
    expect(service.error()).toBe('#E5484D');
  });

  it('should have default font value', () => {
    expect(service.font()).toBe('Rajdhani');
  });

  it('should have default shadeApplied value', () => {
    const shade = service.shadeApplied();
    expect(shade.name).toBe('');
    expect(shade.hex).toBe('');
  });

  it('should have defined themes', () => {
    expect(service.definedThemes.length).toBeGreaterThan(0);
    expect(service.definedThemes[0]).toBe(SPARTAN_HUD);
    expect(service.definedThemes[0].name).toBe('Spartan HUD');
  });

  it('resets the picker to the Spartan HUD colours and font', () => {
    service.applyTheme(service.definedThemes[1]);
    service.font.set('Roboto');
    expect(service.primary()).toBe('#FFDE03');

    service.reset();

    expect(service.primary()).toBe(SPARTAN_HUD.primary);
    expect(service.tertiary()).toBe(SPARTAN_HUD.tertiary);
    expect(service.error()).toBe(SPARTAN_HUD.error);
    expect(service.font()).toBe('Rajdhani');
  });

  it('should apply theme correctly', () => {
    const testTheme = {
      name: 'Test',
      primary: '#111111',
      secondary: '#222222',
      tertiary: '#333333',
      neutral: '#444444',
      neutralVariant: '#555555',
      error: '#666666'
    };

    service.applyTheme(testTheme);

    expect(service.primary()).toBe('#111111');
    expect(service.secondary()).toBe('#222222');
    expect(service.tertiary()).toBe('#333333');
    expect(service.neutral()).toBe('#444444');
    expect(service.neutralVariant()).toBe('#555555');
    expect(service.error()).toBe('#666666');
  });

  it('should set primary shade', async () => {
    // Create a mock HTML element
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    await service.setPrimaryShade('#ABCDEF');

    expect(service.shadeApplied().name).toBe('primary');
    expect(service.shadeApplied().hex).toBe('#ABCDEF');
  });

  it('should set secondary shade', async () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    await service.setSecondaryShade('#123456');

    expect(service.shadeApplied().name).toBe('secondary');
    expect(service.shadeApplied().hex).toBe('#123456');
  });

  it('should set tertiary shade', async () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    await service.setTertiaryShade('#FEDCBA');

    expect(service.shadeApplied().name).toBe('tertiary');
    expect(service.shadeApplied().hex).toBe('#FEDCBA');
  });

  it('should set neutral shade', async () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    await service.setNeutralShade('#AABBCC');

    expect(service.shadeApplied().name).toBe('neutral');
    expect(service.shadeApplied().hex).toBe('#AABBCC');
  });

  it('should set neutral variant shade', async () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    await service.setNeutralVariantShade('#DDEEFF');

    expect(service.shadeApplied().name).toBe('neutral-variant');
    expect(service.shadeApplied().hex).toBe('#DDEEFF');
  });

  it('should set error shade', async () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    await service.setErrorShade('#FF0000');

    expect(service.shadeApplied().name).toBe('error');
    expect(service.shadeApplied().hex).toBe('#FF0000');
  });

  it('should handle missing root element when setting shade', async () => {
    spyOn(document, 'querySelector').and.returnValue(null);
    spyOn(console, 'log');

    await service.setPrimaryShade('#ABCDEF');

    expect(console.log).toHaveBeenCalledWith('root missing');
  });

  it('should set font properties when root exists', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setFont('Arial');

    // Verify that style properties were set
    expect(mockRoot.style.getPropertyValue('--sys-body-small-font')).toBe('Arial');
    expect(mockRoot.style.getPropertyValue('--sys-label-large-font')).toBe('Arial');
  });

  it('should return early when root is null for setFont', () => {
    spyOn(document, 'querySelector').and.returnValue(null);

    // Should not throw error
    expect(() => service.setFont('Arial')).not.toThrow();
  });

  it('sets the seed colour at once and its tonal palette once the colour utilities load', async () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    const done = service.setTertiaryShade('#FFD25E');
    expect(mockRoot.style.getPropertyValue('--tertiary')).toBe('#FFD25E');
    await done;

    expect(mockRoot.style.getPropertyValue('--tertiary-0')).toBe('#000000');
    expect(mockRoot.style.getPropertyValue('--tertiary-100')).toBe('#ffffff');
    expect(mockRoot.style.getPropertyValue('--tertiary-50')).not.toBe('');
  });

  it('loads the Roboto stylesheet only when Roboto is picked, and only once', () => {
    const links = () => document.head.querySelectorAll('link[href*="family=Roboto"]');
    links().forEach(l => l.remove());

    service.setFont('Rajdhani');
    expect(links().length).toBe(0);

    service.setFont('Roboto');
    service.setFont('Roboto');
    expect(links().length).toBe(1);
    expect((links()[0] as HTMLLinkElement).rel).toBe('stylesheet');
    links().forEach(l => l.remove());
  });
});
