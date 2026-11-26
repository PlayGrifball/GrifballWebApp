import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { SpartanCatalog, SpartanChoice, SpartanProfileDto } from '@api/models';
import { MyProfileService } from '@api/services/my-profile.service';
import { problemText } from '@shared/errorText';

/** The signed-in user's Spartan: what shows (their choice or their equipped set), the render service's catalog to choose from, and the render. One per panel. */
@Injectable()
export class ProfileSpartanStore {
  private readonly api = inject(MyProfileService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _profile = signal<SpartanProfileDto | null>(null);
  readonly profile = this._profile.asReadonly();
  private readonly _catalog = signal<SpartanCatalog | null>(null);
  readonly catalog = this._catalog.asReadonly();
  private readonly _busy = signal(false);
  readonly busy = this._busy.asReadonly();

  load(): void {
    this.api.getMySpartan().subscribe({
      next: profile => this._profile.set(profile),
      error: (error: HttpErrorResponse) => this.snackBar.open(problemText(error, "Couldn't load your Spartan."), 'Close'),
    });
  }

  loadCatalog(): void {
    if (this._catalog()) return;
    this.api.getSpartanCatalog().subscribe({
      next: catalog => this._catalog.set(catalog),
      error: (error: HttpErrorResponse) => this.snackBar.open(problemText(error, "The Spartan catalog isn't available right now."), 'Close'),
    });
  }

  save(choice: SpartanChoice): void {
    this.run(this.api.setMySpartan({ body: choice }), 'Spartan saved. It renders in a few minutes.', "Couldn't save your Spartan.");
  }

  reset(): void {
    this.run(this.api.clearMySpartan(), 'Your equipped Spartan shows again.', "Couldn't reset your Spartan.");
  }

  render(): void {
    this.run(this.api.renderMySpartan(), 'Render asked for. It takes a few minutes.', "Couldn't ask for a render.");
  }

  private run(call: import('rxjs').Observable<unknown>, done: string, failed: string): void {
    this._busy.set(true);
    call.subscribe({
      next: () => {
        this._busy.set(false);
        this.snackBar.open(done, 'Close');
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this._busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
