import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
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

  /** Which of the two models the 3D view shows; the one there is when there is none of the other. */
  readonly pose = signal<'neutral' | 'stance'>('neutral');
  readonly modelName = computed(() => {
    const wanted = `${this.pose()}-glb`;
    return this.hasFile(wanted) ? wanted : ['neutral-glb', 'stance-glb'].find(n => this.hasFile(n)) ?? null;
  });

  /** The render's time is in the address, so a new render is not shown from the cache of an old one. */
  readonly fileUrl = (name: string): string => {
    const rendered = this.profile()?.renderedAt;
    const version = rendered ? `?v=${encodeURIComponent(rendered)}` : '';
    return `/api/users/${this.userID()}/spartan/${name}${version}`;
  };
  readonly hasFile = (name: string): boolean => !!this.profile()?.files?.includes(name);
}
