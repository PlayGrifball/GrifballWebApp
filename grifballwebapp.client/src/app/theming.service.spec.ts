import { TestBed } from '@angular/core/testing';
import { ThemingService } from './theming.service';

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
    expect(service.primary()).toBe('#f35700');
    expect(service.secondary()).toBe('#8e0003');
    expect(service.tertiary()).toBe('#000582');
    expect(service.neutral()).toBe('#919093');
    expect(service.neutralVariant()).toBe('#8E9098');
    expect(service.error()).toBe('#FF5449');
  });

  it('should have default font value', () => {
    expect(service.font()).toBe('Roboto');
  });

  it('should have default shadeApplied value', () => {
    const shade = service.shadeApplied();
    expect(shade.name).toBe('');
    expect(shade.hex).toBe('');
  });

  it('should have defined themes', () => {
    expect(service.definedThemes.length).toBeGreaterThan(0);
    expect(service.definedThemes[0].name).toBe('Default');
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

  it('should set primary shade', () => {
    // Create a mock HTML element
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setPrimaryShade('#ABCDEF');

    expect(service.shadeApplied().name).toBe('primary');
    expect(service.shadeApplied().hex).toBe('#ABCDEF');
  });

  it('should set secondary shade', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setSecondaryShade('#123456');

    expect(service.shadeApplied().name).toBe('secondary');
    expect(service.shadeApplied().hex).toBe('#123456');
  });

  it('should set tertiary shade', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setTertiaryShade('#FEDCBA');

    expect(service.shadeApplied().name).toBe('tertiary');
    expect(service.shadeApplied().hex).toBe('#FEDCBA');
  });

  it('should set neutral shade', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setNeutralShade('#AABBCC');

    expect(service.shadeApplied().name).toBe('neutral');
    expect(service.shadeApplied().hex).toBe('#AABBCC');
  });

  it('should set neutral variant shade', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setNeutralVariantShade('#DDEEFF');

    expect(service.shadeApplied().name).toBe('neutral-variant');
    expect(service.shadeApplied().hex).toBe('#DDEEFF');
  });

  it('should set error shade', () => {
    const mockRoot = document.createElement('html');
    spyOn(document, 'querySelector').and.returnValue(mockRoot);

    service.setErrorShade('#FF0000');

    expect(service.shadeApplied().name).toBe('error');
    expect(service.shadeApplied().hex).toBe('#FF0000');
  });

  it('should handle missing root element when setting shade', () => {
    spyOn(document, 'querySelector').and.returnValue(null);
    spyOn(console, 'log');

    service.setPrimaryShade('#ABCDEF');

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
});
