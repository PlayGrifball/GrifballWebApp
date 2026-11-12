import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeasonRefDto } from '@api/models';

/** Seasons as a comma-separated list, each linked to its page: "Winter League 2019 (2019), ...". */
@Component({
  selector: 'app-season-links',
  imports: [RouterLink],
  template: `@for (s of seasons(); track s.seasonID; let last = $last) {<a [routerLink]="['/season', s.seasonID]">{{s.seasonName}} ({{s.year}})</a>{{ last ? '' : ', ' }}}`,
  // One box, broken between seasons, never inside a name: wrapped in running text it does not sit over the words before it
  styles: ':host { display: inline-block; } a { white-space: nowrap; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonLinksComponent {
  readonly seasons = input.required<SeasonRefDto[]>();
}
