import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LineChartComponent } from './lineChart.component';
import type { MetricSeriesDto } from '../../api/dtos/telemetryDto';

describe('LineChartComponent', () => {
  let fixture: ComponentFixture<LineChartComponent>;
  const start = Date.parse('2026-11-03T11:00:00Z');
  const end = Date.parse('2026-11-03T12:00:00Z');

  const el = () => fixture.nativeElement as HTMLElement;
  const text = (e: Element | null) => (e?.textContent ?? '').replace(/\s+/g, ' ').trim();

  function render(series: MetricSeriesDto[], to = end): void {
    fixture = TestBed.createComponent(LineChartComponent);
    fixture.componentRef.setInput('series', series);
    fixture.componentRef.setInput('unit', 'MB');
    fixture.componentRef.setInput('start', start);
    fixture.componentRef.setInput('end', to);
    fixture.detectChanges();
  }

  /** The pointer at a fraction of the plot's width (the SVG stubbed to 600 px, its viewBox' width). */
  function point(fraction: number): void {
    const svg = el().querySelector('svg')!;
    spyOn(svg, 'getBoundingClientRect').and.returnValue(new DOMRect(0, 0, 600, 170));
    svg.dispatchEvent(new PointerEvent('pointermove', { clientX: 48 + fraction * (600 - 48 - 8) }));
    fixture.detectChanges();
  }

  afterEach(() => fixture.destroy());

  it('draws a line per series, scaled to a round axis', () => {
    render([
      { name: 'grif-backend-1', points: [[start, 100], [end, 150]] },
      { name: 'grif-worker-1', points: [[start, 120]] },
    ]);

    const lines = Array.from(el().querySelectorAll('polyline'));
    expect(lines.length).toBe(2);
    expect(lines[0].getAttribute('points')).toBe('48.0,78.0 592.0,43.0');
    expect(Array.from(el().querySelectorAll('text')).map(text).slice(0, 3)).toEqual(['200', '100', '0']);
    expect(Array.from(el().querySelectorAll('.lc-key')).map(text)).toEqual(['grif-backend-1: 150 MB', 'grif-worker-1: 120 MB']);
  });

  it('shows the values under the pointer, none where a series has no point near', () => {
    render([
      { name: 'requests', points: [[start, 1], [start + 1800_000, 2], [end, 3]] },
      { name: 'early only', points: [[start, 5], [start + 60_000, 6]] },
    ]);

    point(0.5);
    expect(el().querySelector('.lc-cursor')).not.toBeNull();
    const legend = text(el().querySelector('.lc-legend'));
    expect(legend).toContain('requests: 2 MB');
    expect(legend).toContain('early only: - MB');

    el().querySelector('svg')!.dispatchEvent(new PointerEvent('pointerleave'));
    fixture.detectChanges();
    expect(el().querySelector('.lc-cursor')).toBeNull();
  });

  it('dates the axis of a range over a day and a half', () => {
    render([{ name: 'a', points: [] }], start + 7 * 86400_000);

    expect(text(el().querySelectorAll('text')[3])).toMatch(/^Nov \d+, /);
    expect(text(el().querySelector('.lc-legend'))).toBe('a');
  });
  it('ignores the pointer over an unlaid chart, and has no value for an empty series', () => {
    render([{ name: 'empty', points: [] }]);
    const svg = el().querySelector('svg')!;
    spyOn(svg, 'getBoundingClientRect').and.returnValue(new DOMRect(0, 0, 0, 0));
    svg.dispatchEvent(new PointerEvent('pointermove', { clientX: 10 }));
    fixture.detectChanges();
    expect(el().querySelector('.lc-cursor')).toBeNull();

    (svg.getBoundingClientRect as jasmine.Spy).and.returnValue(new DOMRect(0, 0, 600, 170));
    svg.dispatchEvent(new PointerEvent('pointermove', { clientX: 300 }));
    fixture.detectChanges();
    expect(text(el().querySelector('.lc-legend'))).toContain('empty: - MB');
  });
  it('draws a zero-length range, and reads a lone point wherever the pointer is', () => {
    render([{ name: 'once', points: [[start, 4]] }], start);
    expect(el().querySelector('polyline')!.getAttribute('points')).toBe('48.0,36.0');

    point(0.9);
    expect(text(el().querySelector('.lc-legend'))).toContain('once: 4 MB');
  });
});
