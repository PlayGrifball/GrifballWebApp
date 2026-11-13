import { ChangeDetectionStrategy, Component, input, OnInit, signal, inject } from '@angular/core';
import { ExcelFormComponent } from './excelForm/excelForm.component';
import { ExcelService } from '@api/services/excel.service';
import type { SheetDto } from '@api/models';

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
  private excel = inject(ExcelService);


  inputSpreadsheetId = input<string>('');
  inputSheetName = input<string>('');
  
  sheetInfo = signal<SheetDto[]>([]);

  ngOnInit(): void {

    if (this.inputSpreadsheetId() && this.inputSheetName()) {
      this.sheetInfo.set([{
        name: '',
        spreadsheetID: this.inputSpreadsheetId(),
        sheetName: this.inputSheetName(),
      }]);
    } else {
      this.excel.defaultSheetDto()
      .subscribe({
        next: (v) => {
          this.sheetInfo.set(v);
        }
      });
    }
  }

}
