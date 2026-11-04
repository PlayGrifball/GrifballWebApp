import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { deploymentFinishLabel, type DeploymentHistoryDto } from '../../api/dtos/deploymentHistoryDto';

/**
 * A profile's deployment history: each season the player played, newest first, with their team, whether they
 * captained it, the team's record and how far it went. Nothing at all when they were on no team.
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

  readonly historyResource = rxResource({
    params: () => this.userID(),
    stream: ({ params }) => this.http.get<DeploymentHistoryDto[]>(`/api/DeploymentHistory/GetDeploymentHistory/${params}`),
  });
  readonly history = computed<DeploymentHistoryDto[]>(() => this.historyResource.hasValue() ? this.historyResource.value() ?? [] : []);

  readonly finishLabel = deploymentFinishLabel;
}
