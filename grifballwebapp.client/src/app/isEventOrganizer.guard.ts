import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { map } from 'rxjs';
import { AccountService } from './account.service';

/** Commissioners only. Waits until the API has said who is signed in (the session is a cookie the app cannot read). */
export const isEventOrganizerGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  return accountService.whenKnown().pipe(map(() => accountService.isEventOrganizer()));
};
