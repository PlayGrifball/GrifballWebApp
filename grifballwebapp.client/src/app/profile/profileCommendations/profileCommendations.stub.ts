import { Component, input } from '@angular/core';

/** For the profile page's spec: the commendations panel is tested on its own (profileCommendations.component.spec.ts),
 * so the page's spec only checks what it hands it, and makes no request for it. */
@Component({ selector: 'app-profile-commendations', template: '' })
export class StubProfileCommendationsComponent {
  readonly userID = input<number>();
}
