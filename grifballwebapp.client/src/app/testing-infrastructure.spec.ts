import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

// Simple integration test to verify basic testing infrastructure
describe('Testing Infrastructure', () => {
  
  it('should be able to test Angular signals', () => {
    const testSignal = signal('initial value');
    expect(testSignal()).toBe('initial value');
    
    testSignal.set('updated value');
    expect(testSignal()).toBe('updated value');
  });

  it('should be able to test with Angular TestBed', () => {
    TestBed.configureTestingModule({});
    expect(TestBed).toBeDefined();
  });

  it('should be able to create spy objects', () => {
    const mockObject = jasmine.createSpyObj('MockObject', ['method1', 'method2']);
    mockObject.method1.and.returnValue('mocked return');
    
    expect(mockObject.method1()).toBe('mocked return');
    expect(mockObject.method1).toHaveBeenCalled();
  });

  it('should support async testing patterns', (done) => {
    const start = Date.now();
    setTimeout(() => {
      expect(Date.now() - start).toBeGreaterThanOrEqual(5);
      done();
    }, 10);
  });
});