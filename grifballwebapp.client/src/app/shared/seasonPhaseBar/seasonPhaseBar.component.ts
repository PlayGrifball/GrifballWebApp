import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { SeasonPhaseStep } from '../seasonEvents';

/** A season's timeline: signups, draft and the rest, each marked done, active or still to come. */
@Component({
  selector: 'app-season-phase-bar',
  imports: [DatePipe],
  template: `
    <ol class="hud-phase-bar" [attr.aria-label]="label()">
      @for (step of steps(); track step.eventType) {
        <li class="hud-phase-step" [class.hud-phase-step--active]="step.state === 'active'" [class.hud-phase-step--done]="step.state === 'done'">
          <span class="hud-phase-step__name">{{step.title}}</span>
          <span class="hud-phase-step__meta">
            @if (step.state === 'active') { Active · ends {{step.end | date:'MMM d'}} }
            @else if (step.state === 'done') { Completed · {{step.end | date:'MMM d'}} }
            @else { Starts {{step.start | date:'MMM d'}} }
          </span>
        </li>
      }
    </ol>
  `,
  styles: `:host { display: block; }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonPhaseBarComponent {
  readonly steps = input.required<SeasonPhaseStep[]>();
  readonly label = input('Season timeline');
}
