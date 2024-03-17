import { type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AccountService } from './account.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const accountService = inject(AccountService);
  const jwt = accountService.jwt();

  if (jwt === null) {
    return next(req);
  }

  const authReq = req.clone({ setHeaders: { Authorization: "Bearer " + jwt } });

  return next(authReq);
};
