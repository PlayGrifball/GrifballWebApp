import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { SeasonAwardDto } from '@api/models';

/** The season's awards (its MVP, named awards, records): each award with its winners, a player linked to their profile and a team to its page. */
@Component({
  selector: 'app-season-awards',
  imports: [RouterLink],
  template: `
    <section class="awards hud-panel hud-panel--chamfer" aria-labelledby="season-awards">
      <h2 id="season-awards" class="hud-h2 awards__title">Awards</h2>
      <dl class="awards__grid">
        @for (award of awards(); track award.name) {
          <div class="award">
            <dt class="award__name">{{award.name}}</dt>
            @for (winner of award.winners; track $index) {
              <dd class="award__winner">
                @if (winner.userID != null) {
                  <a [routerLink]="['/profile', winner.userID]">{{winner.name}}</a>
                } @else if (winner.teamID != null) {
                  <a [routerLink]="['/season', seasonID(), 'team', winner.teamID]">{{winner.name}}</a>
                } @else {
                  {{winner.name}}
                }
                @if (winner.userID != null && winner.teamID != null) {
                  <a class="award__team" [routerLink]="['/season', seasonID(), 'team', winner.teamID]">{{winner.teamName}}</a>
                }
                @if (winner.value) {
                  <span class="award__value">{{winner.value}}</span>
                }
              </dd>
            }
          </div>
        }
      </dl>
    </section>
  `,
  styleUrl: './seasonAwards.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonAwardsComponent {
  readonly awards = input.required<SeasonAwardDto[]>();
  readonly seasonID = input.required<number>();
}
