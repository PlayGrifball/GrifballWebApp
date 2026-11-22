import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { map } from 'rxjs';
import { AccountService } from './account.service';

/** Commissioners or sysadmins (the match page's own "isStaff", and the stat flags API's role check). Waits until the
 * API has said who is signed in (the session is a cookie the app cannot read). */
export const isStaffGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  return accountService.whenKnown().pipe(map(() => accountService.isEventOrganizer() || accountService.isSysAdmin()));
};
