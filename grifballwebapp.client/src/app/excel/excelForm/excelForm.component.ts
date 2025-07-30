import { ChangeDetectionStrategy, Component, input, OnInit, signal, WritableSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '../../validation/directives/regexMatchValidValidatorDirective.directive';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SheetInfo } from '../excel.component';
import { HttpClient, HttpParams } from '@angular/common/http';

@Component({
  selector: 'app-excel-form',
  imports: [
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    RegexMatchValidValidatorDirective,
    MatProgressSpinnerModule
  ],
  templateUrl: './excelForm.component.html',
  styleUrl: './excelForm.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExcelFormComponent implements OnInit {

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

  constructor(private httpClient: HttpClient) {}

  isSubmittingMatch: WritableSignal<boolean> = signal(false);

  onSubmit(matchID: string): void {
    this.isSubmittingMatch.set(true);

    let params = new HttpParams().set('matchID', matchID);
    let dto: SheetInfo = {
      name: this.inputName(),
      spreadsheetID: this.spreadsheetID,
      sheetName: this.sheetName,
    }

    this.httpClient.post('/api/Excel/AppendMatch/', dto, { params })
        .subscribe({
          next: result => console.log(result),
          error: result => console.log(result),
        }).add(() => this.isSubmittingMatch.set(false));
  }
}
