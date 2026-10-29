import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { SeasonTabsComponent } from './seasonTabs.component';

describe('SeasonTabsComponent', () => {
  let fixture: ComponentFixture<SeasonTabsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonTabsComponent],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(SeasonTabsComponent);
    fixture.componentRef.setInput('seasonID', 7);
    fixture.componentRef.setInput('active', 'schedule');
    fixture.detectChanges();
  });

  const links = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'));

  it('links every section of the season', () => {
    expect(links().map(a => [a.textContent!.trim(), a.getAttribute('href')])).toEqual([
      ['Overview', '/season/7'],
      ['Schedule', '/season/7?tab=schedule'],
      ['Bracket', '/season/7?tab=bracket'],
      ['Teams', '/season/7/teams'],
      ['Player grades', '/season/7/playergrades'],
      ['Signups', '/season/7/signups'],
    ]);
  });

  it('marks only the active section as the current page', () => {
    const current = links().filter(a => a.getAttribute('aria-current') === 'page');
    expect(current.map(a => a.textContent!.trim())).toEqual(['Schedule']);
    expect(current[0].classList).toContain('hud-tab--active');
  });
});

describe('SeasonTabsComponent without a season input', () => {
  it("links the season of the page's route", async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonTabsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '9' } } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(SeasonTabsComponent);
    fixture.componentRef.setInput('active', 'signups');
    fixture.detectChanges();
    const first = (fixture.nativeElement as HTMLElement).querySelector('a')!;
    expect(first.getAttribute('href')).toBe('/season/9');
  });
});
