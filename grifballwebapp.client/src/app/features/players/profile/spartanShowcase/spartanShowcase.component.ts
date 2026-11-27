import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import type { SpartanProfileDto } from '@api/models';
import { SpartanViewerComponent } from '../spartanViewer/spartanViewer.component';

/** A user's rendered Spartan: the pictures and the 3D model that anyone can turn around. */
@Component({
  selector: 'app-spartan-showcase',
  imports: [SpartanViewerComponent],
  templateUrl: './spartanShowcase.component.html',
  styleUrl: './spartanShowcase.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpartanShowcaseComponent {
  readonly userID = input.required<number>();
  readonly profile = input.required<SpartanProfileDto | null>();
  readonly name = input('Spartan');

  /** Which pose the one 3D model is in. */
  readonly pose = signal<'neutral' | 'stance'>('neutral');

  /** The render's time is in the address, so a new render is not shown from the cache of an old one. */
  readonly fileUrl = (name: string): string => {
    const rendered = this.profile()?.renderedAt;
    const version = rendered ? `?v=${encodeURIComponent(rendered)}` : '';
    return `/api/users/${this.userID()}/spartan/${name}${version}`;
  };
  readonly hasFile = (name: string): boolean => !!this.profile()?.files?.includes(name);
}
