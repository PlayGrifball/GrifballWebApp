import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

export type SeasonTab = 'overview' | 'schedule' | 'bracket' | 'teams' | 'grades' | 'signups';

interface TabLink {
  id: SeasonTab;
  title: string;
  link: (string | number)[];
  queryParams?: Record<string, string>;
}

/** The section links shared by the season page and its sub-pages. */
@Component({
  selector: 'app-season-tabs',
  imports: [RouterLink],
  template: `
    <nav class="hud-tabs" aria-label="Season sections">
      @for (tab of tabs(); track tab.id) {
        <a class="hud-tab" [class.hud-tab--active]="tab.id === active()"
           [attr.aria-current]="tab.id === active() ? 'page' : null"
           [routerLink]="tab.link" [queryParams]="tab.queryParams">{{tab.title}}</a>
      }
    </nav>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonTabsComponent {
  private route = inject(ActivatedRoute, { optional: true });

  /** The season; by default the :seasonID of the page's route. */
  readonly seasonID = input<number>();
  private readonly season = computed(() => this.seasonID() ?? Number(this.route?.snapshot?.paramMap?.get('seasonID') ?? 0));
  readonly active = input.required<SeasonTab>();

  readonly tabs = computed<TabLink[]>(() => {
    const season = ['/season', this.season()];
    return [
      { id: 'overview', title: 'Overview', link: season },
      { id: 'schedule', title: 'Schedule', link: season, queryParams: { tab: 'schedule' } },
      { id: 'bracket', title: 'Bracket', link: season, queryParams: { tab: 'bracket' } },
      { id: 'teams', title: 'Teams', link: [...season, 'teams'] },
      { id: 'grades', title: 'Player grades', link: [...season, 'playergrades'] },
      { id: 'signups', title: 'Signups', link: [...season, 'signups'] },
    ];
  });
}
