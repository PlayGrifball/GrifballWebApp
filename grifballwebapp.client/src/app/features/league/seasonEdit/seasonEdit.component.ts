import { Component, OnInit, ChangeDetectionStrategy, computed, inject, input, linkedSignal, numberAttribute, viewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SeasonSettings } from '../api/seasonSettings';
import { seasonFormats } from '@shared/seasonFormats';
import { FormsModule, NgForm } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MtxDatetimepickerModule, } from '@ng-matero/extensions/datetimepicker';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSelectModule } from '@angular/material/select';
import { provideDatePickers } from '@shared/datePickers';
import { SeasonEditStore } from './seasonEdit.store';

/** A new season: in the main league, its game still to choose. */
const newSeason = (): SeasonSettings => ({ leagueID: 1, gameVersionID: null, division: null, format: 'LeagueSeason', copyFromSeasonID: null } as SeasonSettings);

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
    providers: [provideDatePickers(), SeasonEditStore],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonEditComponent implements OnInit {
  private readonly store = inject(SeasonEditStore);

  readonly seasonForm = viewChild.required<NgForm>('seasonForm');

  /** The :seasonID of the route (0: a new season). Angular keeps this page when only it changes, so a new one resets
   * the form to that season. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });

  /** The form: the season once it arrives, a new one before (and for 0); empty again for each season of the route. */
  readonly model = linkedSignal<{ seasonID: number, season: SeasonSettings | null }, SeasonSettings>({
    source: () => ({ seasonID: this.seasonID(), season: this.store.season() }),
    computation: ({ season }) => season ?? newSeason(),
  });

  /** The formats a season can have. */
  readonly formats = seasonFormats;

  /** The games and leagues a season can have (empty if they fail to load). */
  readonly gameVersions = this.store.gameVersions;
  readonly leagues = this.store.leagues;

  /** Every season, for "Copy from" (chosen by its ID). */
  private readonly seasons = this.store.seasons;
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
  patch(changes: Partial<SeasonSettings>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }

  ngOnInit(): void {
    this.store.loadOptions();
  }

  onSubmit() {
    if (!this.seasonForm().valid) {
      return;
    }

    this.store.save(this.model(), seasonID => this.patch({ seasonID }));
  }
}
