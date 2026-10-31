import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PaletteComponent } from './palette/palette.component';
import { MatFormField, MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ThemingService } from '../theming.service';

@Component({
    selector: 'app-theme',
    imports: [
        PaletteComponent,
        MatFormFieldModule,
        MatSelectModule,
        ReactiveFormsModule,
        MatButtonModule,
        MatIconModule,
    ],
    templateUrl: './theme.component.html',
    styleUrl: './theme.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ThemeComponent implements OnInit {
  private themingService = inject(ThemingService);
  /** Set when shown as the header's dialog (not on the /theme page): it then has Close and Done. */
  readonly dialogRef = inject(MatDialogRef<ThemeComponent>, { optional: true });

  /** Closes the dialog; the focus goes back to the button that opened it. */
  close(): void {
    this.dialogRef?.close();
  }


  ngOnInit(): void {
    const initialValue = this.themingService.font();

    this.font = new FormControl<string>(initialValue, { nonNullable: true });

    this.font.valueChanges.subscribe({
      next: newFont => {
        console.log('setting font');
        this.themingService.font.set(newFont);
      },
    })
  }

  fonts: string[] = ['Rajdhani', 'Roboto', 'sans-serif'];
  /** Counts resets: the palette editors are rebuilt on each one. */
  readonly resets = signal(0);

  /** Back to the Spartan HUD colours and font. */
  reset(): void {
    this.themingService.reset();
    this.font.setValue(this.themingService.font(), { emitEvent: false });
    this.resets.update(n => n + 1);
  }
  font!: FormControl<string>;
}
