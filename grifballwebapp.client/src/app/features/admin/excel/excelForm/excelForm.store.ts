import { DestroyRef, inject, Injectable, computed, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { OnDemandJobStatusDto, SheetDto } from '@api/models';
import { ExcelService } from '@api/services/excel.service';
import { Subscription, switchMap, takeWhile, timer } from 'rxjs';

/** How often a queued or running export of every match is looked at again. */
export const EXPORT_POLL_MS = 3000;

/**
 * Writing matches to a sheet: one match appended, or every match in the copy sheet exported (a job on the worker,
 * followed until it's done). One per form (the component provides it).
 */
@Injectable()
export class ExcelFormStore {
  private readonly excel = inject(ExcelService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _submittingMatch = signal(false);
  readonly submittingMatch = this._submittingMatch.asReadonly();

  /** The export of every match in the copy sheet: where it is, null until one is asked for. */
  private readonly _exportRun = signal<OnDemandJobStatusDto | null>(null);
  readonly exportRun = this._exportRun.asReadonly();
  readonly exporting = computed(() => this.exportRun()?.state === 'Queued' || this.exportRun()?.state === 'Running');
  private readonly _exportError = signal<string | null>(null);
  readonly exportError = this._exportError.asReadonly();
  /** An export asked for and not yet answered: another waits for it. */
  private startingExport = false;
  private exportWatch?: Subscription;

  /** Appends a match to the sheet; the answer, or the failure, is logged. */
  appendMatch(matchID: string, sheet: SheetDto): void {
    this._submittingMatch.set(true);
    this.excel.appendMatch({ matchID, body: sheet })
      .subscribe({
        next: result => console.log(result),
        error: result => console.log(result),
      }).add(() => this._submittingMatch.set(false));
  }

  /** Queues the export of every match in the copy sheet to this sheet (replacing its rows), then follows it until it's done. */
  exportAll(sheet: SheetDto): void {
    if (this.startingExport)
      return;
    this.startingExport = true;
    this._exportError.set(null);
    this.excel.exportAll({ body: sheet })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => {
          this.startingExport = false;
          this.followExport(status);
        },
        error: () => {
          this.startingExport = false;
          this._exportError.set("Couldn't start the export.");
        },
      });
  }

  private followExport(status: OnDemandJobStatusDto): void {
    this._exportRun.set(status);
    // Only the latest export is followed
    this.exportWatch?.unsubscribe();
    if (status.state === 'Idle')
      return;
    this.exportWatch = timer(EXPORT_POLL_MS, EXPORT_POLL_MS).pipe(
      switchMap(() => this.excel.exportAllStatus()),
      takeWhile(next => next.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => this._exportRun.set(next),
      error: () => this._exportError.set("Couldn't tell whether the export finished."),
    });
  }
}
