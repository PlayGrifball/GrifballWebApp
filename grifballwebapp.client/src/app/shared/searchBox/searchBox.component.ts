import { ChangeDetectionStrategy, Component, model, OnDestroy } from '@angular/core';
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
export class SearchBoxComponent implements OnDestroy {
  private inputSubject = new Subject<string>();
  value = model.required<string>();

  constructor() {
    this.inputSubject.pipe(
      debounceTime(300) // 300ms delay
    ).subscribe(val => {
      this.value.set(val.trim());
    });
  }

  onInput(event: Event) {
      const input = event.target as HTMLInputElement;
      this.inputSubject.next(input.value);
    }
  
  ngOnDestroy() {
    this.inputSubject.complete();
  }
}
