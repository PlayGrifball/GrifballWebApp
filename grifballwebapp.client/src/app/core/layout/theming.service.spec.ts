import { TestBed } from '@angular/core/testing';
import { ARCHIVE_VAULT, SPARTAN_HUD, ThemingService } from './theming.service';

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
    // Archive Vault
    expect(service.era()).toBe('');
    expect(service.primary()).toBe('#E8922D');
    expect(service.secondary()).toBe('#2C3344');
    expect(service.tertiary()).toBe('#E6C15A');
    expect(service.neutral()).toBe('#6B768F');
    expect(service.neutralVariant()).toBe('#5C667D');
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
    expect(service.definedThemes[0]).toBe(ARCHIVE_VAULT);
    expect(service.definedThemes[0].name).toBe('Archive Vault');
    expect(service.definedThemes).toContain(SPARTAN_HUD);
  });

  it('resets the picker to the Archive Vault colours and font', () => {
    service.applyTheme(SPARTAN_HUD);
    service.font.set('Roboto');
    expect(service.primary()).toBe(SPARTAN_HUD.primary);
    expect(service.era()).toBe('spartan-hud');

    service.reset();

    expect(service.era()).toBe(ARCHIVE_VAULT.era);
    expect(service.primary()).toBe(ARCHIVE_VAULT.primary);
    expect(service.tertiary()).toBe(ARCHIVE_VAULT.tertiary);
    expect(service.error()).toBe(ARCHIVE_VAULT.error);
    expect(service.font()).toBe('Rajdhani');
  });

  it('sets and clears the data-theme-era attribute on <html>', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setEra('2008');
    expect(mockRoot.getAttribute('data-theme-era')).toBe('2008');

    service.setEra('');
    expect(mockRoot.getAttribute('data-theme-era')).toBeNull();
  });

  it('leaves the era alone when the page has no <html> element', () => {
    spyOn(document, 'querySelector').and.returnValue(null);
    expect(() => service.setEra('2008')).not.toThrow();
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
    spyOn(console, 'warn');

    await service.setPrimaryShade('#ABCDEF');

    expect(console.warn).toHaveBeenCalledWith('Theme: no <html> element to set the shade on');
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
    expect(links()).toHaveSize(0);

    service.setFont('Roboto');
    service.setFont('Roboto');
    expect(links()).toHaveSize(1);
    expect((links()[0] as HTMLLinkElement).rel).toBe('stylesheet');
    links().forEach(l => l.remove());
  });
});
