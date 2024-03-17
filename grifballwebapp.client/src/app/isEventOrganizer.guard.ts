import type { ActivatedRouteSnapshot, CanActivateFn, RouterStateSnapshot } from '@angular/router';
import { AccountService } from './account.service';
import { inject } from '@angular/core';

export const isEventOrganizerGuard: CanActivateFn = (route : ActivatedRouteSnapshot, state : RouterStateSnapshot) => {
  const accountService = inject(AccountService)
  return accountService.isEventOrganizer();
};
