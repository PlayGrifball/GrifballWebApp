import { ChangeDetectionStrategy, Component, computed, input, OnInit, inject } from '@angular/core';
import { ExcelFormComponent } from './excelForm/excelForm.component';
import type { SheetDto } from '@api/models';
import { ExcelStore } from './excel.store';

@Component({
  selector: 'app-excel',
  imports: [
    ExcelFormComponent,
  ],
  templateUrl: './excel.component.html',
  styleUrl: './excel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ExcelStore],
})
export class ExcelComponent implements OnInit {
  private readonly store = inject(ExcelStore);

  inputSpreadsheetId = input<string>('');
  inputSheetName = input<string>('');

  /** The sheet the inputs name, when they name one. */
  private readonly given = computed<SheetDto | null>(() => this.inputSpreadsheetId() && this.inputSheetName()
    ? { name: '', spreadsheetID: this.inputSpreadsheetId(), sheetName: this.inputSheetName() }
    : null);
  /** The sheet the inputs name, or else the default sheets. */
  readonly sheetInfo = computed<SheetDto[]>(() => {
    const given = this.given();
    return given ? [given] : this.store.defaults();
  });

  ngOnInit(): void {
    if (!this.given())
      this.store.loadDefaults();
  }
}
