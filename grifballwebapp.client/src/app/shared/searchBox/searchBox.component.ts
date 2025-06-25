import { ChangeDetectionStrategy, Component, input, model, OnDestroy } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, Subject } from 'rxjs';

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
  private inputSubject = new Subject<T>();
  value = model.required<T>();
  type = input<'text' | 'number'>('text');

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
        const parsedValue = parseInt(input.value);
        if (!isNaN(parsedValue)) {
          this.inputSubject.next(parsedValue as unknown as T);
        } else {
          console.log('Invalid number input');
        }
      } else {
        this.inputSubject.next(input.value as T);
      }
    }
  
  ngOnDestroy() {
    this.inputSubject.complete();
  }
}
