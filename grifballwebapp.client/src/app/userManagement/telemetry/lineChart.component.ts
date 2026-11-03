import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import type { MetricSeriesDto } from '../../api/dtos/telemetryDto';

const WIDTH = 600;
const HEIGHT = 170;
const LEFT = 48;
const RIGHT = 8;
const TOP = 8;
const BOTTOM = 22;

/** One color per series, in order; the HUD's own, so they follow the theme. */
export const SERIES_COLORS = ['var(--hud-accent)', 'var(--hud-gold)', 'var(--hud-red-text)', 'var(--hud-blue-text)', 'var(--hud-muted-2)'];

/** A value as the charts and tables show it: three significant figures, whole numbers from 100. */
export function formatValue(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 100 || value === 0)
    return Math.round(value).toLocaleString('en-US');
  return Number(value.toPrecision(3)).toString();
}

/** The axis' top: the largest value rounded up to 1, 2, 2.5 or 5 times a power of ten (1 when there are none). */
export function niceMax(max: number): number {
  if (!(max > 0))
    return 1;
  const power = Math.pow(10, Math.floor(Math.log10(max)));
  const step = [1, 2, 2.5, 5, 10].find(s => s * power >= max)!;
  return step * power;
}

/**
 * A small line chart for the Telemetry page's Overview: one line per series over [start, end], drawn in SVG (no chart
 * library), with the value under the pointer.
 */
@Component({
  selector: 'app-line-chart',
  template: `
    <svg class="lc-svg" [attr.viewBox]="'0 0 ' + width + ' ' + height" role="img" [attr.aria-label]="label()"
         (pointermove)="hover($event)" (pointerleave)="hovered.set(null)">
      @for (tick of ticks(); track $index) {
        <line class="lc-grid" [attr.x1]="left" [attr.x2]="width - right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
        <text class="lc-axis" [attr.x]="left - 6" [attr.y]="tick.y + 4" text-anchor="end">{{tick.label}}</text>
      }
      <text class="lc-axis" [attr.x]="left" [attr.y]="height - 6">{{timeLabel(start())}}</text>
      <text class="lc-axis" [attr.x]="width - right" [attr.y]="height - 6" text-anchor="end">{{timeLabel(end())}}</text>
      @for (line of lines(); track line.name) {
        <polyline class="lc-line" [attr.points]="line.points" [style.stroke]="line.color" />
      }
      @if (hovered(); as h) {
        <line class="lc-cursor" [attr.x1]="h.x" [attr.x2]="h.x" [attr.y1]="top" [attr.y2]="height - bottom" />
      }
    </svg>
    <div class="lc-legend">
      @if (hovered(); as h) {
        <span class="lc-time">{{timeLabel(h.time, true)}}</span>
        @for (v of h.values; track v.name) {
          <span class="lc-key"><i [style.background]="v.color"></i>{{v.name}}: {{v.value === null ? '-' : formatValue(v.value)}} {{unit()}}</span>
        }
      } @else {
        @for (line of lines(); track line.name) {
          <span class="lc-key"><i [style.background]="line.color"></i>{{line.name}}@if (line.last !== null) {: {{formatValue(line.last)}} {{unit()}}}</span>
        }
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .lc-svg { display: block; width: 100%; height: auto; touch-action: pan-y; }
    .lc-grid { stroke: var(--hud-hairline); stroke-width: 1; }
    .lc-cursor { stroke: var(--hud-muted); stroke-width: 1; stroke-dasharray: 3 3; }
    .lc-axis { fill: var(--hud-muted); font-size: 11px; font-family: var(--hud-font-mono); }
    .lc-line { fill: none; stroke-width: 1.75; vector-effect: non-scaling-stroke; stroke-linejoin: round; }
    .lc-legend { display: flex; flex-wrap: wrap; gap: 4px 14px; min-height: 20px; margin-top: 4px; font-size: 12px; color: var(--hud-muted-2); }
    .lc-time { color: var(--hud-text); }
    .lc-key { display: inline-flex; align-items: center; gap: 6px; }
    .lc-key i { display: inline-block; width: 10px; height: 3px; border-radius: 2px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LineChartComponent {
  readonly series = input.required<MetricSeriesDto[]>();
  readonly unit = input('');
  /** Unix milliseconds. */
  readonly start = input.required<number>();
  readonly end = input.required<number>();
  readonly label = input('');

  readonly width = WIDTH;
  readonly height = HEIGHT;
  readonly left = LEFT;
  readonly right = RIGHT;
  readonly top = TOP;
  readonly bottom = BOTTOM;
  readonly formatValue = formatValue;

  readonly max = computed(() => niceMax(Math.max(0, ...this.series().flatMap(s => s.points.map(p => p[1])))));

  readonly ticks = computed(() => [1, 0.5, 0].map(f => ({ y: this.y(this.max() * f), label: formatValue(this.max() * f) })));

  readonly lines = computed(() => this.series().map((s, i) => ({
    name: s.name,
    color: SERIES_COLORS[i % SERIES_COLORS.length],
    points: s.points.map(([t, v]) => `${this.x(t).toFixed(1)},${this.y(v).toFixed(1)}`).join(' '),
    last: s.points.length ? s.points[s.points.length - 1][1] : null,
  })));

  /** Under the pointer: its time, and each series' nearest point. */
  readonly hovered = signal<{ x: number; time: number; values: { name: string; color: string; value: number | null }[] } | null>(null);

  hover(event: PointerEvent): void {
    const svg = event.currentTarget as SVGSVGElement;
    const box = svg.getBoundingClientRect();
    if (!box.width)
      return;
    const x = Math.min(WIDTH - RIGHT, Math.max(LEFT, (event.clientX - box.left) / box.width * WIDTH));
    const time = this.start() + (x - LEFT) / (WIDTH - LEFT - RIGHT) * (this.end() - this.start());
    this.hovered.set({
      x,
      time,
      values: this.series().map((s, i) => ({ name: s.name, color: SERIES_COLORS[i % SERIES_COLORS.length], value: nearest(s.points, time) })),
    });
  }

  timeLabel(ms: number, withDate = false): string {
    const long = withDate || this.end() - this.start() > 36 * 3600_000;
    return new Date(ms).toLocaleString('en-US', long
      ? { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }
      : { hour: 'numeric', minute: '2-digit' });
  }

  private x(time: number): number {
    const span = this.end() - this.start() || 1;
    return LEFT + (time - this.start()) / span * (WIDTH - LEFT - RIGHT);
  }

  private y(value: number): number {
    return TOP + (1 - value / this.max()) * (HEIGHT - TOP - BOTTOM);
  }
}

/** The value of the point closest to the time, if one is within a few steps of it. */
function nearest(points: [number, number][], time: number): number | null {
  if (!points.length)
    return null;
  let best = points[0];
  for (const p of points)
    if (Math.abs(p[0] - time) < Math.abs(best[0] - time))
      best = p;
  const step = points.length > 1 ? (points[points.length - 1][0] - points[0][0]) / (points.length - 1) : Infinity;
  return Math.abs(best[0] - time) <= step * 2 ? best[1] : null;
}
