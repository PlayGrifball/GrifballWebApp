import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, OnInit, WritableSignal } from '@angular/core';
import { TileComponent } from './tile/tile.component';
import { palette } from '../paletteTypes';
import { MtxColorpickerModule } from '@ng-matero/extensions/colorpicker';
import { MatFormFieldModule, MatLabel } from '@angular/material/form-field';
import { ThemingService } from '../../theming.service';
import { MatInput } from '@angular/material/input';
import { FormControl, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-palette',
  standalone: true,
  imports: [
    CommonModule,
    TileComponent,
    MtxColorpickerModule,
    MatFormFieldModule,
    MatLabel,
    MatInput,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './palette.component.html',
  styleUrl: './palette.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaletteComponent implements OnInit {
  paletteName = input.required<palette>();

  constructor(private themingService: ThemingService) {
  }
  
  private getSignal(paletteName: palette): WritableSignal<string> | null {
    switch(paletteName) {
      case 'primary': return this.themingService.primary;
      case 'secondary': return this.themingService.secondary;
      case 'tertiary': return this.themingService.tertiary;
      case 'neutral': return this.themingService.neutral;
      case 'neutral-variant': return this.themingService.neutralVariant;
      case 'error': return this.themingService.error;
      default: return null;
    }
  }
  
  color!: FormControl<string>;

  ngOnInit(): void {
    const initialValue = document.querySelector('html')?.style.getPropertyValue(`--${this.paletteName()}`) ?? '';
    this.color = new FormControl<string>(initialValue, { nonNullable: true });

    this.color.valueChanges.subscribe({
      next: newColor => {
        console.log('setting color');
        const signal = this.getSignal(this.paletteName());
        signal?.set(newColor);
      },
    })
  }


  range = computed(() => {
    const arr: number[] = [];
    for (let i = 100; i >= 0; i -= 10) {
      arr.push(i);
    }
    return arr;
  });

}
