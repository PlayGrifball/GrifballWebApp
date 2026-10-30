import { ApplicationRef, EnvironmentInjector, EnvironmentProviders, inject, provideEnvironmentInitializer } from '@angular/core';

/** How long a snack bar stays up when the code opening it does not say. */
export const SNACK_BAR_DURATION_MS = 5000;

/**
 * Makes snack bars close themselves after SNACK_BAR_DURATION_MS by default.
 *
 * A provider for MAT_SNACK_BAR_DEFAULT_OPTIONS would have to import that token, and with it the
 * snack bar and overlay code, into the app's first download. Instead, once the app is stable
 * (first page shown), the snack bar code is loaded and the duration is set on the app-wide
 * default options (one shared object, which every MatSnackBar reads on each open). No page can
 * open a snack bar before then, as each needs its own code loaded and an answer from the API.
 */
export function provideSnackBarDefaults(): EnvironmentProviders {
  return provideEnvironmentInitializer(() => {
    const injector = inject(EnvironmentInjector);
    inject(ApplicationRef).whenStable()
      .then(() => import('@angular/material/snack-bar'))
      .then(({ MAT_SNACK_BAR_DEFAULT_OPTIONS }) => {
        if (!injector.destroyed)
          injector.get(MAT_SNACK_BAR_DEFAULT_OPTIONS).duration = SNACK_BAR_DURATION_MS;
      });
  });
}
