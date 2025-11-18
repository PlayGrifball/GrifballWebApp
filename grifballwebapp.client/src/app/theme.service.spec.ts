import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    // Clear localStorage before each test
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should initialize with empty theme when localStorage is empty', () => {
    expect(service.currentTheme()).toBe('');
  });

  it('should load theme from localStorage on init', () => {
    localStorage.setItem('theme', 'light-theme');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const newService = TestBed.inject(ThemeService);
    
    expect(newService.currentTheme()).toBe('light-theme');
  });

  it('should ignore invalid theme in localStorage', () => {
    localStorage.setItem('theme', 'invalid-theme');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const newService = TestBed.inject(ThemeService);
    
    expect(newService.currentTheme()).toBe('');
  });

  it('should have previousTheme signal initialized to empty string', () => {
    expect(service.previousTheme()).toBe('');
  });

  describe('applyTheme', () => {
    it('should add theme class to body', () => {
      const bodyElement = document.body;
      const initialClasses = Array.from(bodyElement.classList);
      
      service.applyTheme('light-theme');
      
      expect(bodyElement.classList.contains('light-theme')).toBe(true);
      
      // Clean up
      bodyElement.classList.remove('light-theme');
    });

    it('should remove previous theme class before adding new one', () => {
      const bodyElement = document.body;
      service.previousTheme.set('light-theme');
      bodyElement.classList.add('light-theme');
      
      service.applyTheme('');
      
      expect(bodyElement.classList.contains('light-theme')).toBe(false);
    });

    it('should not try to remove empty string as class', () => {
      const bodyElement = document.body;
      service.previousTheme.set('');
      
      // Should not throw error
      expect(() => service.applyTheme('light-theme')).not.toThrow();
    });
  });

  describe('buttonClicked', () => {
    it('should update previousTheme to current theme', () => {
      service.currentTheme.set('light-theme');
      
      service.buttonClicked();
      
      expect(service.previousTheme()).toBe('light-theme');
    });

    it('should toggle theme from empty to light-theme', () => {
      service.currentTheme.set('');
      
      service.buttonClicked();
      
      expect(service.currentTheme()).toBe('light-theme');
    });

    it('should toggle theme from light-theme to empty', () => {
      service.currentTheme.set('light-theme');
      
      service.buttonClicked();
      
      expect(service.currentTheme()).toBe('');
    });

    it('should save theme to localStorage', () => {
      service.currentTheme.set('');
      
      service.buttonClicked();
      
      expect(localStorage.getItem('theme')).toBe('light-theme');
    });
  });

  describe('next', () => {
    it('should return light-theme when current is empty', () => {
      service.currentTheme.set('');
      
      expect(service.next()).toBe('light-theme');
    });

    it('should return empty when current is light-theme', () => {
      service.currentTheme.set('light-theme');
      
      expect(service.next()).toBe('');
    });

    it('should return empty for unknown themes', () => {
      service.currentTheme.set('' as any);
      
      const result = service.next();
      
      expect(typeof result).toBe('string');
    });
  });
});
