import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { SeasonChampionDto } from '../../api/dtos/seasonSummaryDto';

/** The season's champions: each tier's winning team and the players who hold its title. */
@Component({
  selector: 'app-season-champions',
  imports: [RouterLink],
  template: `
    <section class="champions hud-panel hud-panel--chamfer" aria-labelledby="season-champions">
      <h2 id="season-champions" class="hud-h2 champions__title">Champions</h2>
      <div class="champions__grid">
        @for (champ of champions(); track champ.tier + champ.teamID) {
          <div class="champion">
            <span class="champion__tier">{{champ.tier}} champions</span>
            <a class="champion__team" [routerLink]="['/season', seasonID(), 'team', champ.teamID]">{{champ.teamName}}</a>
            <ul class="champion__roster">
              @for (player of champ.roster; track player.userID) {
                <li>
                  <a [routerLink]="['/profile', player.userID]">{{player.gamertag}}</a>
                  @if (player.isCaptain) {
                    <span class="champion__captain">Captain</span>
                  }
                </li>
              }
            </ul>
          </div>
        }
      </div>
    </section>
  `,
  styleUrl: './seasonChampions.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonChampionsComponent {
  readonly champions = input.required<SeasonChampionDto[]>();
  readonly seasonID = input.required<number>();
}
