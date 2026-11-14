import { ChangeDetectionStrategy, Component, input, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '@shared/validation/directives/regexMatchValidValidatorDirective.directive';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import type { SheetDto } from '@api/models';
import { ExcelFormStore } from './excelForm.store';

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
  providers: [ExcelFormStore],
})
export class ExcelFormComponent implements OnInit {
  private readonly store = inject(ExcelFormStore);

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

  readonly isSubmittingMatch = this.store.submittingMatch;

  /** The sheet the form names. */
  private sheet(): SheetDto {
    return { name: this.inputName(), spreadsheetID: this.spreadsheetID, sheetName: this.sheetName };
  }

  onSubmit(matchID: string): void {
    this.store.appendMatch(matchID, this.sheet());
  }

  /** The export of every match in the copy sheet (a job on the worker): where it is, null until one is asked for. */
  readonly exportRun = this.store.exportRun;
  readonly exporting = this.store.exporting;
  readonly exportError = this.store.exportError;

  /** Queues the export of every match in the copy sheet to this sheet (replacing its rows), then follows it until it's done. */
  exportAll(): void {
    this.store.exportAll(this.sheet());
  }
}
