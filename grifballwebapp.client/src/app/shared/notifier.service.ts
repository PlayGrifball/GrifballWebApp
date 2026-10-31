import { Injectable, Injector, inject } from '@angular/core';
import { SNACK_BAR_DURATION_MS } from './snackBarDefaults';

/**
 * Shows short messages in a snack bar, for code that runs at startup (e.g. AccountService): the
 * snack bar and its overlay are loaded on the first message, not with the app. Pages loaded on
 * demand can inject MatSnackBar themselves.
 */
@Injectable({ providedIn: 'root' })
export class Notifier {
  private injector = inject(Injector);

  async open(message: string, action: string): Promise<void> {
    const { MatSnackBar } = await import('@angular/material/snack-bar');
    // The app-wide default duration may not be set yet this early (see snackBarDefaults.ts).
    this.injector.get(MatSnackBar).open(message, action, { duration: SNACK_BAR_DURATION_MS });
  }
}
