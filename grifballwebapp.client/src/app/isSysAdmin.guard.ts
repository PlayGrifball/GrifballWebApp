import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { map } from 'rxjs';
import { AccountService } from './account.service';

/** Sysadmins only. Waits until the API has said who is signed in (the session is a cookie the app cannot read). */
export const isSysAdminGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  return accountService.whenKnown().pipe(map(() => accountService.isSysAdmin()));
};
