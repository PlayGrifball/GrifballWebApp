import { Component, OnInit, ChangeDetectionStrategy, inject, viewChild, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';
import { FormsModule, NgForm } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { MtxDatetimepickerModule, } from '@ng-matero/extensions/datetimepicker';
import { MatCheckboxModule } from '@angular/material/checkbox';

@Component({
    selector: 'app-season-edit',
    imports: [
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        MtxDatetimepickerModule,
        RouterModule,
        MatCheckboxModule
    ],
    templateUrl: './seasonEdit.component.html',
    styleUrl: './seasonEdit.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonEditComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);

  readonly seasonForm = viewChild.required<NgForm>('seasonForm');

  readonly model = signal<SeasonDto>({} as SeasonDto);

  /** Form edits replace the model rather than mutating it. */
  patch(changes: Partial<SeasonDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  ngOnInit(): void {
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Fetch from backend
    if (seasonID > 0) {
      this.api.getSeason(seasonID)
        .subscribe({
          next: (result) => this.model.set(result),
        });
      return;
    }

    // New record
  }

  onSubmit() {
    if (!this.seasonForm().valid) {
      return;
    }

    this.api.upsertSeason(this.model()).subscribe({
      next: (result) => this.model.update(model => ({ ...model, seasonID: result })),
      error: (result) => console.log(result),
      });
  }
}
