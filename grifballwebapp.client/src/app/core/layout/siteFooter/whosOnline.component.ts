import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LiveService } from '../../live/live.service';
import { counted, presenceLabel, STAFF_AREA } from './presencePages';

/**
 * Who is on the site, live: how many (members and guests), and, opened, each page with the members on it by name and
 * its guests, as forums used to show it. From the live hub (LiveService.presence); nothing until it has said.
 */
@Component({
  selector: 'app-whos-online',
  imports: [RouterLink],
  template: `
    @if (snapshot(); as now) {
      <details class="whos-online">
        <summary class="whos-online__summary">
          <span class="whos-online__dot" aria-hidden="true"></span>
          {{ now.online }} online
          <span class="whos-online__split">({{ split() }})</span>
        </summary>
        <ul class="whos-online__pages">
          @for (page of pages(); track page.path) {
            <li class="whos-online__page">
              @if (page.link) {
                <a class="whos-online__where" [routerLink]="page.path">{{ page.label }}</a>
              } @else {
                <span class="whos-online__where">{{ page.label }}</span>
              }
              <span class="whos-online__who">
                @for (member of page.members; track member.userID; let last = $last) {
                  <a [routerLink]="['/profile', member.userID]">{{ member.name || 'A member' }}</a>@if (!last || page.guests > 0) {, }
                }
                @if (page.guests > 0) {
                  {{ page.guestText }}
                }
              </span>
            </li>
          }
        </ul>
      </details>
    }
  `,
  styles: `
    /* The summary's line is kept while the hub has said nothing yet, so the count doesn't push the footer down */
    :host { display: block; min-height: 44px; }
    .whos-online {
      font-family: var(--hud-font-mono);
      font-size: 13px;
      color: var(--hud-muted);
    }
    .whos-online__summary {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      min-height: 44px;
      cursor: pointer;
    }
    .whos-online__summary:hover { color: var(--hud-heading); }
    .whos-online__dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--hud-accent, currentColor);
    }
    .whos-online__pages {
      list-style: none;
      margin: 0 0 8px;
      padding: 0;
      display: grid;
      gap: 4px;
    }
    .whos-online__page {
      display: flex;
      flex-wrap: wrap;
      gap: 0 8px;
      overflow-wrap: anywhere;
    }
    .whos-online__where { color: var(--hud-heading); }
    .whos-online a {
      color: inherit;
      text-decoration: underline;
      text-underline-offset: 2px;
    }
    .whos-online a:hover { color: var(--hud-heading); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WhosOnlineComponent {
  private readonly live = inject(LiveService);

  readonly snapshot = this.live.presence;

  readonly split = computed(() => {
    const now = this.snapshot();
    return now ? `${counted(now.members, 'member')}, ${counted(now.guests, 'guest')}` : '';
  });

  readonly pages = computed(() => (this.snapshot()?.pages ?? []).map(page => ({
    path: page.path,
    label: presenceLabel(page.path),
    link: page.path !== STAFF_AREA,
    members: page.members,
    guests: page.guests,
    guestText: counted(page.guests, 'guest'),
  })));
}
