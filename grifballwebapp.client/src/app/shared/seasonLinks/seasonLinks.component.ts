import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { SeasonRefDto } from '../../api/dtos/seasonRefDto';

/** Seasons as a comma-separated list, each linked to its page: "Winter League 2019 (2019), ...". */
@Component({
  selector: 'app-season-links',
  imports: [RouterLink],
  template: `@for (s of seasons(); track s.seasonID; let last = $last) {<a [routerLink]="['/season', s.seasonID]">{{s.seasonName}} ({{s.year}})</a>{{ last ? '' : ', ' }}}`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonLinksComponent {
  readonly seasons = input.required<SeasonRefDto[]>();
}
