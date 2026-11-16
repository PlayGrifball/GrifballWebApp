import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/core';
import { Clipboard } from '@angular/cdk/clipboard';
import { calendarFeedUrl, CalendarSubscribeComponent, webcalUrl } from './calendarSubscribe.component';

describe('calendarFeedUrl', () => {
  it('is the feed under the site\'s /api', () => {
    expect(calendarFeedUrl('https://grif.test', 'teams/5/calendar.ics')).toBe('https://grif.test/api/teams/5/calendar.ics');
  });

  it('takes a path with a leading slash as well', () => {
    expect(calendarFeedUrl('https://grif.test', '/seasons/2/calendar.ics')).toBe('https://grif.test/api/seasons/2/calendar.ics');
  });
});

describe('webcalUrl', () => {
  it('swaps https for webcal, which a calendar app opens to subscribe', () => {
    expect(webcalUrl('https://grif.test/api/teams/5/calendar.ics')).toBe('webcal://grif.test/api/teams/5/calendar.ics');
  });

  it('swaps http for webcal too', () => {
    expect(webcalUrl('http://localhost:4200/api/teams/5/calendar.ics')).toBe('webcal://localhost:4200/api/teams/5/calendar.ics');
  });
});

describe('CalendarSubscribeComponent', () => {
  let fixture: ComponentFixture<CalendarSubscribeComponent>;
  let clipboard: jasmine.SpyObj<Clipboard>;

  beforeEach(() => {
    clipboard = jasmine.createSpyObj<Clipboard>('Clipboard', ['copy']);
    clipboard.copy.and.returnValue(true);
    TestBed.configureTestingModule({ providers: [{ provide: Clipboard, useValue: clipboard }] });
    fixture = TestBed.createComponent(CalendarSubscribeComponent);
    fixture.componentRef.setInput('path', 'teams/5/calendar.ics');
    fixture.componentRef.setInput('description', 'This team\'s matches');
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const origin = () => TestBed.inject(DOCUMENT).location.origin;

  it('offers the feed\'s address to copy, on this site', () => {
    const input = el().querySelector<HTMLInputElement>('input')!;
    expect(input.value).toBe(`${origin()}/api/teams/5/calendar.ics`);
    expect(input.readOnly).toBeTrue();
    expect(input.getAttribute('aria-label')).toBe('Calendar feed address');
  });

  it('links to the feed as webcal://, for a calendar app to subscribe', () => {
    const link = el().querySelector<HTMLAnchorElement>('a.cal-sub__webcal')!;
    expect(link.getAttribute('href')).toBe(webcalUrl(`${origin()}/api/teams/5/calendar.ics`));
  });

  it('says what the feed holds', () => {
    expect(el().textContent).toContain('This team\'s matches');
    expect(el().querySelector('summary')!.textContent).toContain('Subscribe to calendar');
  });

  it('copies the address and says so', () => {
    const copy = el().querySelector<HTMLButtonElement>('button.cal-sub__copy')!;
    expect(copy.textContent!.trim()).toBe('Copy');

    copy.click();
    fixture.detectChanges();

    expect(clipboard.copy).toHaveBeenCalledOnceWith(`${origin()}/api/teams/5/calendar.ics`);
    expect(copy.textContent!.trim()).toBe('Copied');
  });

  it('keeps offering to copy when the browser refuses', () => {
    clipboard.copy.and.returnValue(false);
    const copy = el().querySelector<HTMLButtonElement>('button.cal-sub__copy')!;

    copy.click();
    fixture.detectChanges();

    expect(copy.textContent!.trim()).toBe('Copy');
  });

  it('follows a new path', () => {
    fixture.componentRef.setInput('path', 'users/9/calendar.ics');
    fixture.detectChanges();

    expect(el().querySelector<HTMLInputElement>('input')!.value).toBe(`${origin()}/api/users/9/calendar.ics`);
  });
});
