import { afterNextRender, ChangeDetectionStrategy, Component, DestroyRef, effect, ElementRef, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { SPARTAN_SCENE, SpartanScene } from './spartanScene';

/** A rendered Spartan model (a GLB) in 3D that the visitor can turn around. */
@Component({
  selector: 'app-spartan-viewer',
  templateUrl: './spartanViewer.component.html',
  styleUrl: './spartanViewer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpartanViewerComponent {
  private readonly createScene = inject(SPARTAN_SCENE);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly scene = signal<SpartanScene | null>(null);

  /** The model's address. */
  readonly url = input.required<string>();
  /** The clip of the model to pose it with, such as neutral or stance; the model as it comes when there is none. */
  readonly clip = input<string | null>(null);
  readonly label = input('Your Spartan in 3D');
  /** The names of the shown model's clips, each time a model is shown. */
  readonly clipNames = output<readonly string[]>();

  /** loading until the model is shown; unavailable when the browser cannot draw it (no WebGL) or the model cannot be read. */
  readonly state = signal<'loading' | 'ready' | 'unavailable'>('loading');

  constructor() {
    let destroyed = false;
    inject(DestroyRef).onDestroy(() => {
      destroyed = true;
      this.scene()?.dispose();
    });
    afterNextRender(async () => {
      try {
        const scene = await this.createScene(this.canvas().nativeElement);
        if (destroyed) scene.dispose();
        else this.scene.set(scene);
      } catch {
        this.state.set('unavailable');
      }
    });
    effect(onCleanup => {
      const scene = this.scene();
      const url = this.url();
      if (!scene) return;
      const clip = untracked(this.clip);
      if (clip) scene.play(clip);
      let current = true;
      onCleanup(() => { current = false; });
      this.state.set('loading');
      scene.show(url).then(
        names => {
          if (!current) return;
          this.state.set('ready');
          this.clipNames.emit(names);
        },
        () => { if (current) this.state.set('unavailable'); });
    });
    effect(() => {
      const clip = this.clip();
      if (clip) untracked(this.scene)?.play(clip);
    });
  }
}
