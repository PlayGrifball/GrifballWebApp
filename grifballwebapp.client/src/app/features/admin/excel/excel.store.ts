import { inject, Injectable, signal } from '@angular/core';
import type { SheetDto } from '@api/models';
import { ExcelService } from '@api/services/excel.service';

/** The sheets the Excel page writes to by default. One per page (the component provides it). */
@Injectable()
export class ExcelStore {
  private readonly excel = inject(ExcelService);

  /** The default sheets; none until they arrive. */
  private readonly _defaults = signal<SheetDto[]>([]);
  readonly defaults = this._defaults.asReadonly();

  /** Loads the default sheets; a failure is logged, and leaves none. */
  loadDefaults(): void {
    this.excel.defaultSheetInfo().subscribe({
      next: sheets => this._defaults.set(sheets),
      error: error => console.log(error),
    });
  }
}
