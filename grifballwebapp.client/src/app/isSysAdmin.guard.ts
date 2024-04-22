import { inject } from '@angular/core';
import type { ActivatedRouteSnapshot, CanActivateFn, RouterStateSnapshot } from '@angular/router';
import { AccountService } from './account.service';

export const isSysAdminGuard: CanActivateFn = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const accountService = inject(AccountService)
  return accountService.isSysAdmin();
};
