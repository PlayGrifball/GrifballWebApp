import { Component, OnInit, ChangeDetectionStrategy, computed, inject, viewChild, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';
import type { GameVersionDto, LeagueDto } from '../api/dtos/optionDtos';
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

  /** A new season: in the main league, its game still to choose. */
  readonly model = signal<SeasonDto>({ leagueID: 1, gameVersionID: null, division: null, copyFromSeasonID: null } as SeasonDto);

  /** The games and leagues a season can have (empty if they fail to load). */
  readonly gameVersions = signal<GameVersionDto[]>([]);
  readonly leagues = signal<LeagueDto[]>([]);

  /** Every season, for "Copy from" (chosen by its ID). */
  private readonly seasons = signal<SeasonDto[]>([]);
  /** The seasons this one can copy from: all but itself. */
  readonly copyableSeasons = computed(() => this.seasons().filter(season => season.seasonID !== this.model().seasonID));
  /**
   * "Copy from"'s options: each season by its name. A name is only unique within its league, so a name that more than
   * one season has also says which league each one is in.
   */
  readonly copyOptions = computed(() => {
    const seasons = this.copyableSeasons();
    const leagueNames = new Map(this.leagues().map(league => [league.leagueID, league.name]));
    const counts = new Map<string, number>();
    for (const season of seasons)
      counts.set(season.seasonName, (counts.get(season.seasonName) ?? 0) + 1);
    return seasons.map(season => ({
      seasonID: season.seasonID,
      label: (counts.get(season.seasonName) ?? 0) > 1
        ? `${season.seasonName} (${leagueNames.get(season.leagueID) ?? 'League ' + season.leagueID})`
        : season.seasonName,
    }));
  });

  /** Form edits replace the model rather than mutating it. */
  patch(changes: Partial<SeasonDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  ngOnInit(): void {
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Without these lists their selects are empty; the rest of the form still works.
    this.api.getGameVersions().subscribe({
      next: (gameVersions) => this.gameVersions.set(gameVersions),
      error: (error) => console.error(error),
    });
    this.api.getLeagues().subscribe({
      next: (leagues) => this.leagues.set(leagues),
      error: (error) => console.error(error),
    });

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
