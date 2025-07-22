import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, input, OnInit, signal, WritableSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { RegexMatchValidValidatorDirective } from '../validation/directives/regexMatchValidValidatorDirective.directive';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';

@Component({
  selector: 'app-excel',
  imports: [
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    RegexMatchValidValidatorDirective,
    MatProgressSpinnerModule
  ],
  templateUrl: './excel.component.html',
  styleUrl: './excel.component.scss',
  //changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExcelComponent implements OnInit {

  inputSpreadsheetId = input<string>('');
  inputSheetName = input<string>('');
  matchID: string = "";
  spreadsheetID: string = "";
  sheetName: string = "";
  regex: string = String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`;
  regexErrorMessage: string = "Must be a valid GUID";


  constructor(private httpClient: HttpClient) {}

  ngOnInit(): void {

    if (this.inputSpreadsheetId() && this.inputSheetName()) {
      this.spreadsheetID = this.inputSpreadsheetId();
      this.sheetName = this.inputSheetName();
    } else {
      this.httpClient.get<SheetInfo>('/api/Excel/DefaultSheetInfo')
    .subscribe({
      next: (v) => {
        this.spreadsheetID = v.spreadsheetID;
        this.sheetName = v.sheetName;
      }
    });
    }

    
  }

  isSubmittingMatch: WritableSignal<boolean> = signal(false);

  onSubmit(matchID: string): void {
    this.isSubmittingMatch.set(true);

    let params = new HttpParams().set('matchID', matchID);
    let dto: SheetInfo = {
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

export interface SheetInfo
{
  spreadsheetID: string,
  sheetName: string,
}