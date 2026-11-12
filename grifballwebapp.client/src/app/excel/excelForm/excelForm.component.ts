import { ChangeDetectionStrategy, Component, computed, DestroyRef, input, OnInit, signal, WritableSignal, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { switchMap, takeWhile, timer } from 'rxjs';
import type { OnDemandJobStatusDto } from '@api/models';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '../../validation/directives/regexMatchValidValidatorDirective.directive';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SheetInfo } from '../excel.component';
import { ExcelService } from '@api/services/excel.service';
import type { SheetInfo as SheetInfoApiDto } from '@api/models';

/** How often a queued or running export of every match is looked at again. */
export const EXPORT_POLL_MS = 3000;

@Component({
  selector: 'app-excel-form',
  imports: [
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    RegexMatchValidValidatorDirective,
    MatProgressSpinnerModule,
    DatePipe,
  ],
  templateUrl: './excelForm.component.html',
  styleUrl: './excelForm.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExcelFormComponent implements OnInit {
  private excel = inject(ExcelService);
  private destroyRef = inject(DestroyRef);


  inputName = input<string>('');
  inputSpreadsheetId = input<string>('');
  inputSheetName = input<string>('');

  ngOnInit(): void {
    this.spreadsheetID = this.inputSpreadsheetId();
    this.sheetName = this.inputSheetName();
  }
  
  matchID: string = "";
  spreadsheetID: string = "";
  sheetName: string = "";
  regex: string = String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`;
  regexErrorMessage: string = "Must be a valid GUID";

  isSubmittingMatch: WritableSignal<boolean> = signal(false);

  onSubmit(matchID: string): void {
    this.isSubmittingMatch.set(true);

    let dto: SheetInfo = {
      name: this.inputName(),
      spreadsheetID: this.spreadsheetID,
      sheetName: this.sheetName,
    }

    // SheetInfo's generated model also has requestedByUserID (the server fills that in itself); the cast
    // keeps the request body exactly as it was.
    this.excel.appendMatch({ matchID, body: dto as unknown as SheetInfoApiDto })
        .subscribe({
          next: result => console.log(result),
          error: result => console.log(result),
        }).add(() => this.isSubmittingMatch.set(false));
  }

  /** The export of every match in the copy sheet (a job on the worker): where it is, null until one is asked for. */
  readonly exportRun = signal<OnDemandJobStatusDto | null>(null);
  readonly exporting = computed(() => this.exportRun()?.state === 'Queued' || this.exportRun()?.state === 'Running');
  readonly exportError = signal<string | null>(null);

  /** Queues the export of every match in the copy sheet to this sheet (replacing its rows), then follows it until it's done. */
  exportAll(): void {
    this.exportError.set(null);
    const dto: SheetInfo = { name: this.inputName(), spreadsheetID: this.spreadsheetID, sheetName: this.sheetName };
    this.excel.exportAll({ body: dto as unknown as SheetInfoApiDto })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.followExport(status),
        error: () => this.exportError.set("Couldn't start the export."),
      });
  }

  private followExport(status: OnDemandJobStatusDto): void {
    this.exportRun.set(status);
    if (status.state === 'Idle')
      return;
    timer(EXPORT_POLL_MS, EXPORT_POLL_MS).pipe(
      switchMap(() => this.excel.exportAllStatus()),
      takeWhile(next => next.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => this.exportRun.set(next),
      error: () => this.exportError.set("Couldn't tell whether the export finished."),
    });
  }
}
