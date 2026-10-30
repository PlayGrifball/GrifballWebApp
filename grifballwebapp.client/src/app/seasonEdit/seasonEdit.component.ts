import { Component, OnInit, ChangeDetectionStrategy, computed, inject, viewChild, signal } from '@angular/core';
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
import { MatSelectModule } from '@angular/material/select';
import { provideDatePickers } from '../shared/datePickers';

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
        MatCheckboxModule,
        MatSelectModule,
    ],
    templateUrl: './seasonEdit.component.html',
    styleUrl: './seasonEdit.component.scss',
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonEditComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);

  readonly seasonForm = viewChild.required<NgForm>('seasonForm');

  readonly model = signal<SeasonDto>({} as SeasonDto);

  /** Every season, for "Copy from" (the server finds the one to copy by its name). */
  private readonly seasons = signal<SeasonDto[]>([]);
  /** The seasons this one can copy from: all but itself. */
  readonly copyableSeasons = computed(() => this.seasons().filter(season => season.seasonID !== this.model().seasonID));

  /** Form edits replace the model rather than mutating it. */
  patch(changes: Partial<SeasonDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  ngOnInit(): void {
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Without the list, "Copy from" only offers to copy nothing; the form still works.
    this.api.getSeasons().subscribe({
      next: (seasons) => this.seasons.set(seasons),
      error: (error) => console.error(error),
    });

    // Fetch from backend
    if (seasonID > 0) {
      this.api.getSeason(seasonID)
        .subscribe({
          // No such season: keep the empty form.
          next: (result) => { if (result) this.model.set(result); },
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
