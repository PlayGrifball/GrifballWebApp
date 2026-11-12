import { Component, OnInit, ChangeDetectionStrategy, computed, effect, inject, input, numberAttribute, untracked, viewChild, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { switchMap } from 'rxjs';
import { EventOrganizerService } from '@api/services';
import type { SeasonDto } from '@api/models';
import { SeasonSettings } from '../api/seasonSettings';
import { nullWhenNotFound } from '../api/nullWhenNotFound';
import { GameVersionOptionDto, LeagueOptionDto } from '@api/models';
import { seasonFormats } from '../api/seasonFormats';
import { FormsModule, NgForm } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { MtxDatetimepickerModule, } from '@ng-matero/extensions/datetimepicker';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSelectModule } from '@angular/material/select';
import { provideDatePickers } from '../shared/datePickers';
import { whileCurrent } from '../shared/whileCurrent';

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
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonEditComponent implements OnInit {
  private api = inject(EventOrganizerService);

  readonly seasonForm = viewChild.required<NgForm>('seasonForm');

  /** The :seasonID of the route (0: a new season). Angular keeps this page when only it changes, so a new one resets
   * the form to that season. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });

  readonly model = signal<SeasonSettings>(newSeason());

  /** The formats a season can have. */
  readonly formats = seasonFormats;

  /** The games and leagues a season can have (empty if they fail to load). */
  readonly gameVersions = signal<GameVersionOptionDto[]>([]);
  readonly leagues = signal<LeagueOptionDto[]>([]);

  /** Every season, for "Copy from" (chosen by its ID). */
  private readonly seasons = signal<SeasonSettings[]>([]);
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
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => this.load(seasonID));
    });
  }

  ngOnInit(): void {
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
    // A list never holds nulls (List<SeasonDto> on the server). Luxon is only loaded with the
    // pages that need it (season editing), not with the app: the parser is imported when the seasons arrive.
    this.api.getSeasons().pipe(switchMap(async seasons => {
      const { parseSeason } = await import('../api/seasonSettings');
      return seasons.map(season => parseSeason(season)!);
    })).subscribe({
      next: (seasons) => this.seasons.set(seasons),
      error: (error) => console.error(error),
    });
  }

  /** The form for this season: empty for a new one, then filled with the season once it arrives. */
  private load(seasonID: number): void {
    this.model.set(newSeason());
    if (seasonID > 0) {
      this.api.getSeason({ seasonID })
        .pipe(nullWhenNotFound(), whileCurrent(seasonID, this.seasonID), switchMap(async season => season && (await import('../api/seasonSettings')).parseSeason(season)))
        .subscribe({
          // No such season: keep the empty form.
          next: (result) => { if (result) this.model.set(result); },
        });
    }
  }

  onSubmit() {
    if (!this.seasonForm().valid) {
      return;
    }

    // SeasonSettings' dates are Luxon DateTimes, which serialize to the API's ISO strings (DateTime.toJSON).
    this.api.upsertSeason({ body: this.model() as unknown as SeasonDto }).subscribe({
      next: (result) => this.model.update(model => ({ ...model, seasonID: result })),
      error: (result) => console.log(result),
      });
  }
}
