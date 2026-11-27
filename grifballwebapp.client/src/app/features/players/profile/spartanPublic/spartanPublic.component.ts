import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { toObservable, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { SpartanProfileDto } from '@api/models';
import { MyProfileService } from '@api/services/my-profile.service';
import { catchError, EMPTY, switchMap } from 'rxjs';
import { SpartanShowcaseComponent } from '../spartanShowcase/spartanShowcase.component';

/** Anyone's Spartan on their profile, in 3D, for every visitor; nothing shows for a user without a render. */
@Component({
  selector: 'app-spartan-public',
  imports: [SpartanShowcaseComponent],
  template: `
    @if (profile(); as p) {
      @if (p.files.length) {
        <section class="hud-section hud-panel hud-panel--soft" aria-label="Spartan">
          <app-spartan-showcase [userID]="userID()" [profile]="p" [name]="name()" />
        </section>
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpartanPublicComponent {
  private readonly api = inject(MyProfileService);

  readonly userID = input.required<number>();
  readonly name = input('Spartan');

  private readonly _profile = signal<SpartanProfileDto | null>(null);
  readonly profile = this._profile.asReadonly();

  constructor() {
    toObservable(this.userID).pipe(
      switchMap(id => {
        this._profile.set(null);
        return this.api.getUserSpartan({ userId: id }).pipe(catchError(() => EMPTY));
      }),
      takeUntilDestroyed(),
    ).subscribe(profile => this._profile.set(profile));
  }
}
