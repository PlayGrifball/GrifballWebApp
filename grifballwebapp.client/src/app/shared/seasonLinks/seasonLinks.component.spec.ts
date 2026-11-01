import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SeasonLinksComponent } from './seasonLinks.component';

describe('SeasonLinksComponent', () => {
  it('links each season to its page, comma separated', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(SeasonLinksComponent);
    fixture.componentRef.setInput('seasons', [
      { seasonID: 4, seasonName: 'Winter League 2019', year: 2019 },
      { seasonID: 9, seasonName: 'Summer League 2020', year: 2020 },
    ]);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const links = Array.from(el.querySelectorAll('a'));
    expect(links.map(a => a.getAttribute('href'))).toEqual(['/season/4', '/season/9']);
    expect(el.textContent).toBe('Winter League 2019 (2019), Summer League 2020 (2020)');
  });
});
