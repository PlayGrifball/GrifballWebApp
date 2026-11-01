import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BackfilledBannerComponent, DataNoticeComponent } from './dataNotice.component';

describe('DataNoticeComponent', () => {
  let fixture: ComponentFixture<DataNoticeComponent>;

  beforeEach(() => {
    fixture = TestBed.createComponent(DataNoticeComponent);
    fixture.componentRef.setInput('message', "Match schedule wasn't recorded for this season.");
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('says why the section is empty, as a note', () => {
    fixture.detectChanges();
    const note = el().querySelector('[role="note"]')!;
    expect(note.textContent).toContain("Match schedule wasn't recorded for this season.");
    expect(el().querySelector('a')).toBeNull();
  });

  it('links the source spreadsheet in a new tab when given', () => {
    fixture.componentRef.setInput('sourceUrl', 'https://docs.google.com/spreadsheets/d/x');
    fixture.detectChanges();
    const link = el().querySelector('a')!;
    expect(link.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/x');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toContain('noopener');
    expect(link.textContent).toContain('Source spreadsheet');
  });
});

describe('BackfilledBannerComponent', () => {
  let fixture: ComponentFixture<BackfilledBannerComponent>;

  beforeEach(() => {
    fixture = TestBed.createComponent(BackfilledBannerComponent);
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('says the season was backfilled and links its spreadsheet', () => {
    fixture.componentRef.setInput('sourceUrl', 'https://docs.google.com/spreadsheets/d/y');
    fixture.detectChanges();
    expect(el().textContent).toContain('Backfilled from historical data');
    expect(el().querySelector('a')!.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/y');
  });

  it('has no link without a spreadsheet', () => {
    fixture.detectChanges();
    expect(el().textContent).toContain('Backfilled from historical data');
    expect(el().querySelector('a')).toBeNull();
  });
});
