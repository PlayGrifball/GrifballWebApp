import { Component, input } from '@angular/core';
import { VodScope } from '../../api/vods';

/** For the specs of the pages that show VODs: the list is tested on its own (vodList.component.spec.ts), so a page's
 * spec only checks what it hands it, and makes no request for it. */
@Component({ selector: 'app-vod-list', template: '' })
export class StubVodListComponent {
  readonly scope = input<VodScope>();
  readonly id = input<number>();
}
