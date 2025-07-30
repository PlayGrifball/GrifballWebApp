import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, input, OnInit, signal } from '@angular/core';
import { ExcelFormComponent } from './excelForm/excelForm.component';

@Component({
  selector: 'app-excel',
  imports: [
    ExcelFormComponent,
  ],
  templateUrl: './excel.component.html',
  styleUrl: './excel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExcelComponent implements OnInit {

  inputSpreadsheetId = input<string>('');
  inputSheetName = input<string>('');
  
  sheetInfo = signal<SheetInfo[]>([]);

  constructor(private httpClient: HttpClient) {}

  ngOnInit(): void {

    if (this.inputSpreadsheetId() && this.inputSheetName()) {
      this.sheetInfo.set([{
        name: '',
        spreadsheetID: this.inputSpreadsheetId(),
        sheetName: this.inputSheetName()
      }]);
    } else {
      this.httpClient.get<SheetInfo[]>('/api/Excel/DefaultSheetInfo')
      .subscribe({
        next: (v) => {
          this.sheetInfo.set(v);
        }
      });
    }
  }

}

export interface SheetInfo
{
  name: string,
  spreadsheetID: string,
  sheetName: string,
}