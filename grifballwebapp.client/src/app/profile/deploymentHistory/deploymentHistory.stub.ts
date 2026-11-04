import { Component, input } from '@angular/core';

/** For the profile's spec: the deployment history is tested on its own (deploymentHistory.component.spec.ts), so the
 * page's spec makes no request for it. */
@Component({ selector: 'app-deployment-history', template: '' })
export class StubDeploymentHistoryComponent {
  readonly userID = input<number>();
}
