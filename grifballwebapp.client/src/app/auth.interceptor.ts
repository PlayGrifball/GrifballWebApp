import { type HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AccountService } from './account.service';
import { catchError, concatMap, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const accountService = inject(AccountService);

  const accessToken = accountService.accessToken();

  // Not logged in so just send the request
  if (accessToken === undefined || req.url === '/api/Identity/Refresh') {
    return next(req);
  }

  // Add bearer token to the request
  const authReq = req.clone({ setHeaders: { Authorization: "Bearer " + accessToken } });

  return next(authReq).pipe(
    catchError((err: any) => {
      const unauthorized = err instanceof HttpErrorResponse && err.status === 401;
      if (unauthorized === false)
        return throwError(() => err);

      // access token was not good so we need to try and get a new one
      const refreshObserve = accountService.refresh();
      if (refreshObserve === null) {
        console.log('No refresh token available');
        return throwError(() => err);
      }

      return refreshObserve
        .pipe(catchError((err: any) => {
          console.log('Unable to retry request, failed to get new access token');
          return throwError(() => err);
        }))
        .pipe(concatMap(r => {
          // Successfully got a new access token, retry the original request with the new token
          console.log('Retrying request with new access token');
          const authReq = req.clone({ setHeaders: { Authorization: "Bearer " + r.accessToken } });
          return next(authReq);
        }));
    })
  );
};
