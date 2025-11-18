import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { authInterceptor } from './auth.interceptor';
import { AccountService } from './account.service';
import { of, throwError } from 'rxjs';

describe('authInterceptor', () => {
  let httpTestingController: HttpTestingController;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockNext: jasmine.Spy<(req: HttpRequest<any>) => any>;

  beforeEach(() => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['refresh'], {
      accessToken: jasmine.createSpy('accessToken')
    });

    mockNext = jasmine.createSpy('next');

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        { provide: AccountService, useValue: mockAccountService }
      ]
    });

    httpTestingController = TestBed.inject(HttpTestingController);
  });

  it('should pass request through when not logged in', () => {
    (mockAccountService.accessToken as jasmine.Spy).and.returnValue(undefined);
    const mockReq = new HttpRequest('GET', '/api/test');
    mockNext.and.returnValue(of({} as HttpEvent<any>));

    TestBed.runInInjectionContext(() => {
      authInterceptor(mockReq, mockNext);
    });

    expect(mockNext).toHaveBeenCalledWith(mockReq);
  });

  it('should pass request through for refresh endpoint', () => {
    (mockAccountService.accessToken as jasmine.Spy).and.returnValue('some-token');
    const mockReq = new HttpRequest('POST', '/api/Identity/Refresh', {});
    mockNext.and.returnValue(of({} as HttpEvent<any>));

    TestBed.runInInjectionContext(() => {
      authInterceptor(mockReq, mockNext);
    });

    expect(mockNext).toHaveBeenCalledWith(mockReq);
  });

  it('should add authorization header when logged in', () => {
    (mockAccountService.accessToken as jasmine.Spy).and.returnValue('test-token');
    const mockReq = new HttpRequest('GET', '/api/test');
    mockNext.and.returnValue(of({} as HttpEvent<any>));

    TestBed.runInInjectionContext(() => {
      authInterceptor(mockReq, mockNext);
    });

    expect(mockNext).toHaveBeenCalled();
    const calledReq = mockNext.calls.mostRecent().args[0];
    expect(calledReq.headers.get('Authorization')).toBe('Bearer test-token');
  });

  it('should pass through non-401 errors', (done) => {
    (mockAccountService.accessToken as jasmine.Spy).and.returnValue('test-token');
    const mockReq = new HttpRequest('GET', '/api/test');
    const error500 = new HttpErrorResponse({ status: 500, statusText: 'Server Error' });
    mockNext.and.returnValue(throwError(() => error500));

    TestBed.runInInjectionContext(() => {
      authInterceptor(mockReq, mockNext).subscribe({
        error: (err) => {
          expect(err.status).toBe(500);
          done();
        }
      });
    });
  });

  it('should attempt refresh on 401 error when refresh token available', (done) => {
    (mockAccountService.accessToken as jasmine.Spy).and.returnValue('old-token');
    const mockReq = new HttpRequest('GET', '/api/test');
    const error401 = new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' });
    
    let callCount = 0;
    mockNext.and.callFake(() => {
      callCount++;
      if (callCount === 1) {
        return throwError(() => error401);
      } else {
        return of({} as HttpEvent<any>);
      }
    });

    mockAccountService.refresh.and.returnValue(of({ accessToken: 'new-token', tokenType: 'Bearer', refreshToken: 'refresh', expiresIn: 3600 }));

    TestBed.runInInjectionContext(() => {
      authInterceptor(mockReq, mockNext).subscribe({
        next: () => {
          expect(mockAccountService.refresh).toHaveBeenCalled();
          expect(callCount).toBe(2);
          done();
        },
        error: () => fail('Should not error when refresh succeeds')
      });
    });
  });

  it('should return error when no refresh token available on 401', (done) => {
    (mockAccountService.accessToken as jasmine.Spy).and.returnValue('test-token');
    const mockReq = new HttpRequest('GET', '/api/test');
    const error401 = new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' });
    mockNext.and.returnValue(throwError(() => error401));
    mockAccountService.refresh.and.returnValue(null);

    TestBed.runInInjectionContext(() => {
      authInterceptor(mockReq, mockNext).subscribe({
        error: (err) => {
          expect(err.status).toBe(401);
          expect(mockAccountService.refresh).toHaveBeenCalled();
          done();
        }
      });
    });
  });
});
