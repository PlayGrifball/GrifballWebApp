import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { deploymentFinishLabel, type DeploymentHistoryDto } from '../../api/dtos/deploymentHistoryDto';

/**
 * A profile's deployment history: each season the player played, newest first, with their team, whether they
 * captained it, the team's record and how far it went. Nothing at all when they were on no team; a short note with a
 * retry when the history could not be loaded.
 */
@Component({
  selector: 'app-deployment-history',
  imports: [RouterLink],
  templateUrl: './deploymentHistory.component.html',
  styleUrl: './deploymentHistory.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeploymentHistoryComponent {
  private http = inject(HttpClient);

  readonly userID = input.required<number>();
  /** The profile's chosen game (its slug), or 'all'. A season played on more than one game has no single slug, so it stays. */
  readonly game = input<string>('all');

  readonly historyResource = rxResource({
    params: () => this.userID(),
    stream: ({ params }) => this.http.get<DeploymentHistoryDto[]>(`/api/DeploymentHistory/GetDeploymentHistory/${params}`),
  });
  readonly history = computed<DeploymentHistoryDto[]>(() => {
    const lines = this.historyResource.hasValue() ? this.historyResource.value() ?? [] : [];
    const game = this.game();
    return game === 'all' ? lines : lines.filter(d => !d.season.gameVersionSlug || d.season.gameVersionSlug === game);
  });

  readonly finishLabel = deploymentFinishLabel;
}
