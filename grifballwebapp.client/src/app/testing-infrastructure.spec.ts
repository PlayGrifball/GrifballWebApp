import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AccountService } from './account.service';

// Simple integration test to verify basic testing infrastructure
describe('Testing Infrastructure', () => {
  
  it('should be able to run basic Jasmine tests', () => {
    expect(true).toBeTruthy();
    expect(false).toBeFalsy();
    expect(2 + 2).toBe(4);
    expect('hello').toContain('ell');
  });

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
    setTimeout(() => {
      expect(true).toBeTruthy();
      done();
    }, 10);
  });
});