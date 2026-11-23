import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonPhaseBarComponent } from './seasonPhaseBar.component';
import { SeasonPhaseStep } from '../seasonEvents';

describe('SeasonPhaseBarComponent', () => {
  let fixture: ComponentFixture<SeasonPhaseBarComponent>;

  const step = (eventType: SeasonPhaseStep['eventType'], title: string, state: SeasonPhaseStep['state']): SeasonPhaseStep =>
    ({ eventType, title, state, start: '2026-11-14T19:00:00Z', end: '2026-12-23T06:04:00Z' });

  beforeEach(() => {
    fixture = TestBed.createComponent(SeasonPhaseBarComponent);
    fixture.componentRef.setInput('steps', [step('Signup', 'Signups', 'done'), step('Draft', 'Draft', 'active'), step('Season', 'Regular season', 'upcoming')]);
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('marks each step done, active or still to come, with its dates', () => {
    fixture.detectChanges();
    const items = [...el().querySelectorAll('li')];
    expect(items.map(li => li.classList.contains('hud-phase-step--done'))).toEqual([true, false, false]);
    expect(items.map(li => li.classList.contains('hud-phase-step--active'))).toEqual([false, true, false]);
    expect(items.map(li => li.querySelector('.hud-phase-step__meta')!.textContent!.trim())).toEqual([
      'Completed · Dec 23', 'Active · ends Dec 23', 'Starts Nov 14',
    ]);
  });

  it('names the timeline for assistive technology, which a page can override', () => {
    fixture.detectChanges();
    expect(el().querySelector('ol')!.getAttribute('aria-label')).toBe('Season timeline');
    fixture.componentRef.setInput('label', 'Current season timeline');
    fixture.detectChanges();
    expect(el().querySelector('ol')!.getAttribute('aria-label')).toBe('Current season timeline');
  });
});
