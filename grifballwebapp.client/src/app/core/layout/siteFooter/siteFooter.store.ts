import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BuildService } from '@api/services/build.service';
import { catchError, map, of } from 'rxjs';

/** Which build is running: its commit and when it was made. One per footer (the component provides it). */
@Injectable()
export class SiteFooterStore {
  private readonly build = inject(BuildService);

  // The build info is decoration: if it cannot be loaded the footer just leaves it out
  // (a toSignal over a failed observable would throw on every read).
  readonly commitHash = toSignal(this.build.getCommitHash().pipe(catchError(() => of(null))), { initialValue: null });
  readonly commitDate = toSignal(this.build.getCommitDate().pipe(map(x => x ? new Date(x) : null), catchError(() => of(null))), { initialValue: null });
}
