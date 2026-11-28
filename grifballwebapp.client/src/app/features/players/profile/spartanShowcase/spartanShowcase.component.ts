import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import type { SpartanProfileDto } from '@api/models';
import { SpartanViewerComponent } from '../spartanViewer/spartanViewer.component';

/** A pose of the 3D model: its clip and what the picker calls it. */
export interface SpartanPose {
  clip: string;
  label: string;
}

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

  /** The clips of the model shown. */
  private readonly clips = signal<readonly string[]>([]);
  /** Which pose (clip) the one 3D model is in. */
  readonly pose = signal('neutral');

  /** Neutral, the user's stance when one was rendered, then each other pose the model carries (the hammer and sword ones) by its name. */
  readonly poses = computed<SpartanPose[]>(() => {
    const clips = this.clips();
    return [
      ...(clips.includes('neutral') ? [{ clip: 'neutral', label: 'Neutral pose' }] : []),
      ...(clips.includes('stance') && this.hasFile('stance-body') ? [{ clip: 'stance', label: 'In stance' }] : []),
      ...clips.filter(c => c !== 'neutral' && c !== 'stance').map(c => ({ clip: c, label: c })),
    ];
  });

  /** The render's time is in the address, so a new render is not shown from the cache of an old one. */
  readonly fileUrl = (name: string): string => {
    const rendered = this.profile()?.renderedAt;
    const version = rendered ? `?v=${encodeURIComponent(rendered)}` : '';
    return `/api/users/${this.userID()}/spartan/${name}${version}`;
  };
  readonly hasFile = (name: string): boolean => !!this.profile()?.files?.includes(name);

  /** A new model that lacks the pose it was in goes back to neutral. */
  shown(clips: readonly string[]): void {
    this.clips.set(clips);
    if (!this.poses().some(p => p.clip === this.pose())) this.pose.set('neutral');
  }

  choose(event: Event): void {
    this.pose.set((event.target as HTMLSelectElement).value);
  }
}
