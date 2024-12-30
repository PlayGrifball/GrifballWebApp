import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { PaletteComponent } from './palette/palette.component';
import { MatFormField, MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ThemingService } from '../theming.service';

@Component({
    selector: 'app-theme',
    imports: [
        CommonModule,
        PaletteComponent,
        MatFormFieldModule,
        MatSelectModule,
        ReactiveFormsModule,
    ],
    templateUrl: './theme.component.html',
    styleUrl: './theme.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ThemeComponent implements OnInit {
  constructor(private themingService: ThemingService) {}

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

  fonts: string[] = ['Roboto', 'sans-serif'];
  font!: FormControl<string>;
}
