import { ChangeDetectionStrategy, Component, input, model, OnDestroy, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, Subject } from 'rxjs';
import { AppLog } from '@core/telemetry/appLog';

@Component({
  selector: 'app-search-box',
  imports: [
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './searchBox.component.html',
  styleUrl: './searchBox.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchBoxComponent<T> implements OnDestroy {
  private readonly log = inject(AppLog);
  private inputSubject = new Subject<T>();
  value = model.required<T>();
  type = input<'text' | 'number'>('text');
  /** The field's label. */
  label = input('Search');

  constructor() {
    this.inputSubject.pipe(
      debounceTime(300) // 300ms delay
    ).subscribe(val => {
      if (typeof val === 'string') {
        this.value.set(val.trim() as T);
      } else {
        this.value.set(val);
      }
    });
  }

  onInput(event: Event) {
      const input = event.target as HTMLInputElement;
      if (this.type() === 'number') {
        const parsedValue = Number.parseInt(input.value);
        if (!Number.isNaN(parsedValue)) {
          this.inputSubject.next(parsedValue as unknown as T);
        } else {
          this.log.debug('Search box: not a number');
        }
      } else {
        this.inputSubject.next(input.value as T);
      }
    }
  
  ngOnDestroy() {
    this.inputSubject.complete();
  }
}
