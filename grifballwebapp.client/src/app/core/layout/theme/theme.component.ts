import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { PaletteComponent } from './palette/palette.component';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ThemingService } from '../theming.service';
import { ThemePersistence } from '../themePersistence.service';
import { AppLog } from '../../telemetry/appLog';

@Component({
    selector: 'app-theme',
    // Rendered on the server with the site's colours: the browser draws it again with the visitor's
    host: { ngSkipHydration: 'true' },
    imports: [
        PaletteComponent,
        MatFormFieldModule,
        MatSelectModule,
        ReactiveFormsModule,
        MatButtonModule,
    ],
    templateUrl: './theme.component.html',
    styleUrl: './theme.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ThemeComponent implements OnInit {
  private readonly log = inject(AppLog);
  private themingService = inject(ThemingService);
  private readonly persistence = inject(ThemePersistence);

  constructor() {
    // The font follows the theme when it is replaced (a preset, a reset, or the account's saved one arriving)
    effect(() => {
      const font = this.themingService.font();
      if (this.font && this.font.value !== font)
        this.font.setValue(font, { emitEvent: false });
    });
  }

  ngOnInit(): void {
    const initialValue = this.themingService.font();

    this.font = new FormControl<string>(initialValue, { nonNullable: true });

    this.font.valueChanges.subscribe({
      next: newFont => {
        this.log.debug('Theme: setting the font');
        this.themingService.font.set(newFont);
      },
    })
  }

  fonts: string[] = ['Rajdhani', 'Roboto', 'sans-serif'];
  readonly themes = this.themingService.definedThemes;
  readonly activeEra = this.themingService.era;
  readonly activePrimary = this.themingService.primary;
  /** Counts resets: the palette editors are rebuilt on each one. */
  readonly resets = signal(0);
  /** Resets and the account's theme arriving: the palette editors are rebuilt on each. */
  readonly generation = computed(() => this.resets() + this.persistence.restored());

  selectTheme(index: number): void {
    const theme = this.themes[index];
    if (!theme)
      return;
    this.themingService.applyTheme(theme);
    this.resets.update(n => n + 1);
  }

  /** Back to the Archive Vault colours and font. */
  reset(): void {
    this.themingService.reset();
    this.resets.update(n => n + 1);
  }
  font!: FormControl<string>;
}
