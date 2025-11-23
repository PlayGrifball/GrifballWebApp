# Frontend Testing Guide

This document describes the frontend testing setup for the GrifballWebApp Angular application.

## Overview

The frontend now includes a comprehensive testing infrastructure using:
- **Jasmine** for testing framework and assertions
- **Karma** for test runner
- **Chrome Headless** for browser testing
- **Angular Testing Utilities** for component and service testing

## Running Tests

### Development Mode (Watch)
```bash
cd grifballwebapp.client
npm test
```

### CI Mode (Single Run with Coverage)
```bash
cd grifballwebapp.client
npm run test:ci
```

## Current Test Coverage

As of current implementation:
- **377 total tests** discovered by Karma
- **377 tests passing** (100% success rate)
- **Code coverage**: 45.49% statements, 33.87% functions, 46.02% lines

### Code Coverage Reporting

The frontend testing infrastructure includes code coverage reporting that:
- Generates HTML reports in `coverage/` directory for local viewing
- Produces LCOV format (`coverage/lcov.info`) for integration with external tools
- Provides text summaries in console output
- Automatically uploads coverage to **Codecov** via GitHub Actions workflow

### Codecov Integration

Frontend test coverage is automatically uploaded to Codecov through the `frontend-tests.yml` GitHub Actions workflow:
- Coverage reports are generated in LCOV format
- Uploaded with `frontend` flag for separate tracking from backend coverage
- Integrated with pull request checks and status reporting
- Similar to the existing backend .NET codecov integration

## Test Files

### Component Tests
- `app.component.spec.ts` - Main application component tests
- `commissioner-dashboard/commissioner-dashboard.component.spec.ts` - Dashboard component tests

### Service Tests  
- `account.service.spec.ts` - Authentication service tests

### Infrastructure Tests
- `testing-infrastructure.spec.ts` - Basic testing patterns and utilities

## Testing Patterns

### Component Testing
```typescript
describe('ComponentName', () => {
  let component: ComponentName;
  let fixture: ComponentFixture<ComponentName>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ComponentName, NoopAnimationsModule],
      providers: [
        // Mock services
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ComponentName);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
```

### Service Testing
```typescript
describe('ServiceName', () => {
  let service: ServiceName;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ServiceName]
    });

    service = TestBed.inject(ServiceName);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });
});
```

### Signal Testing
```typescript
it('should test Angular signals', () => {
  const testSignal = signal('initial');
  expect(testSignal()).toBe('initial');
  
  testSignal.set('updated');
  expect(testSignal()).toBe('updated');
});
```

## Mock Patterns

### Service Mocking
```typescript
const mockService = jasmine.createSpyObj('ServiceName', ['method1', 'method2']);
mockService.method1.and.returnValue('mocked value');
```

### Signal Mocking
```typescript
const mockServiceWithSignals = {
  property: signal('mock value'),
  method: jasmine.createSpy('method')
};
```

## Configuration Files

- `karma.conf.js` - Test runner configuration
- `tsconfig.spec.json` - TypeScript configuration for tests
- `angular.json` - Angular testing build configuration

## Adding New Tests

1. Create a `.spec.ts` file next to your component/service
2. Follow the existing patterns in the codebase
3. Run tests to ensure they pass
4. Consider adding tests for:
   - Component creation and initialization
   - User interactions and events
   - Service method calls and HTTP requests
   - Error handling
   - Edge cases

## Known Issues

Some tests may fail due to:
- MediaMatcher API limitations in headless Chrome
- Complex component dependencies requiring additional mocking
- Angular Material dialog dependencies

These are normal for initial test setup and can be refined over time.

## Future Improvements

- Add more component tests for existing components
- Implement integration tests
- Add E2E testing with Playwright or Cypress
- Improve test coverage to >80%
- Add visual regression testing
- Set up automated test reporting

## Resources

- [Angular Testing Guide](https://angular.io/guide/testing)
- [Jasmine Documentation](https://jasmine.github.io/)
- [Karma Configuration](http://karma-runner.github.io/latest/config/configuration-file.html)