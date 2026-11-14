import { ChangeDetectionStrategy, Component, computed, input, OnInit, signal, inject } from '@angular/core';
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

  /** The sheet the inputs name, when they name one; otherwise the default sheets. */
  private readonly given = signal<SheetDto[] | null>(null);
  readonly sheetInfo = computed(() => this.given() ?? this.store.defaults());

  ngOnInit(): void {
    if (this.inputSpreadsheetId() && this.inputSheetName()) {
      this.given.set([{
        name: '',
        spreadsheetID: this.inputSpreadsheetId(),
        sheetName: this.inputSheetName(),
      }]);
    } else {
      this.store.loadDefaults();
    }
  }
}
