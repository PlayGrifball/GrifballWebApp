import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, TitleStrategy } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DateTime } from 'luxon';
import { canonicalUrl, DEFAULT_DESCRIPTION, describePage, fullTitle, pageDate, PageDescription, PageMeta, PageMetaTitleStrategy, SITE_NAME } from './pageMeta';

const meta = (selector: string) => document.head.querySelector(`meta[${selector}]`)?.getAttribute('content');
const canonical = () => document.head.querySelector('link[rel="canonical"]')?.getAttribute('href');

const described = signal<PageDescription | null>(null);

@Component({ template: '' })
class DescribedPage {
  constructor() {
    describePage(() => described());
  }
}

@Component({ template: '' })
class PlainPage { }

describe('pageMeta', () => {
  afterEach(() => {
    for (const el of Array.from(document.head.querySelectorAll('link[rel="canonical"]')))
      el.remove();
  });

  it('titles a page after itself, then the site', () => {
    expect(fullTitle('Leaderboards')).toBe('Leaderboards · Play Grifball');
    expect(fullTitle('Home')).toBe(SITE_NAME);
    expect(fullTitle(undefined)).toBe(SITE_NAME);
  });

  it('gives the page\'s address without its query or fragment', () => {
    expect(canonicalUrl('https://grifball.xyz', '/match/abc?tab=medals#x')).toBe('https://grifball.xyz/match/abc');
    expect(canonicalUrl('https://grifball.xyz', '/')).toBe('https://grifball.xyz/');
    expect(canonicalUrl('https://grifball.xyz', '')).toBe('https://grifball.xyz/');
    expect(canonicalUrl('https://grifball.xyz', '?x=1')).toBe('https://grifball.xyz/');
  });

  it('writes a date the same everywhere: UTC, in English', () => {
    expect(pageDate(DateTime.fromISO('2024-05-24T01:00:02Z'))).toBe('May 24, 2024');
    expect(pageDate(DateTime.fromISO('2024-05-23T23:30:00-05:00'))).toBe('May 24, 2024');
    expect(pageDate(DateTime.fromISO('nonsense'))).toBeUndefined();
    expect(pageDate(null)).toBeUndefined();
  });

  describe('with the router', () => {
    // The harness renders the routed page (a router alone creates no component without an outlet)
    let router: RouterTestingHarness;

    beforeEach(() => {
      described.set(null);
      TestBed.configureTestingModule({
        providers: [
          provideRouter([
            { path: 'match/:id', component: DescribedPage, title: 'Match Details' },
            { path: 'seasons', component: PlainPage, title: 'Seasons' },
          ]),
          { provide: TitleStrategy, useClass: PageMetaTitleStrategy },
        ],
      });
    });

    beforeEach(async () => {
      router = await RouterTestingHarness.create();
    });

    it('sets the route\'s title, the default description and the page\'s address on each navigation', async () => {
      await router.navigateByUrl('/seasons?page=2');
      const url = location.origin + '/seasons';
      expect(document.title).toBe('Seasons · Play Grifball');
      expect(meta('name="description"')).toBe(DEFAULT_DESCRIPTION);
      expect(meta('property="og:title"')).toBe('Seasons · Play Grifball');
      expect(meta('property="og:description"')).toBe(DEFAULT_DESCRIPTION);
      expect(meta('property="og:url"')).toBe(url);
      expect(meta('name="twitter:title"')).toBe('Seasons · Play Grifball');
      expect(meta('name="twitter:description"')).toBe(DEFAULT_DESCRIPTION);
      expect(canonical()).toBe(url);
      expect(document.head.querySelectorAll('link[rel="canonical"]')).toHaveSize(1);
    });

    it('lets a page describe itself once its data is in, and drops that when it goes', async () => {
      await router.navigateByUrl('/match/abc');
      expect(document.title).toBe('Match Details · Play Grifball');

      described.set({ title: 'Red 5–3 Blue', description: 'Red beat Blue 5–3.' });
      TestBed.tick();
      expect(document.title).toBe('Red 5–3 Blue · Play Grifball');
      expect(meta('name="description"')).toBe('Red beat Blue 5–3.');
      expect(meta('property="og:title"')).toBe('Red 5–3 Blue · Play Grifball');
      expect(canonical()).toBe(location.origin + '/match/abc');

      // Without a description of its own, the site's
      described.set({ title: 'Red 5–3 Blue' });
      TestBed.tick();
      expect(meta('name="description"')).toBe(DEFAULT_DESCRIPTION);

      // Loading another: the route's again until it is in
      described.set(null);
      TestBed.tick();
      expect(document.title).toBe('Match Details · Play Grifball');

      described.set({ title: 'Red 5–3 Blue' });
      TestBed.tick();
      await router.navigateByUrl('/seasons');
      expect(document.title).toBe('Seasons · Play Grifball');
      expect(canonical()).toBe(location.origin + '/seasons');
    });

    it('keeps a page\'s description when the route\'s comes after it', async () => {
      described.set({ title: 'Red 5–3 Blue' });
      await router.navigateByUrl('/match/abc');
      TestBed.tick();
      TestBed.inject(PageMeta).setRoute('Match Details', '/match/abc');
      expect(document.title).toBe('Red 5–3 Blue · Play Grifball');
    });

    it('doesn\'t let a page that is gone clear another\'s', () => {
      const pageMeta = TestBed.inject(PageMeta);
      const gone = {};
      const current = {};
      pageMeta.setRoute('Match Details', '/match/b');
      pageMeta.setPage(gone, { title: 'A' });
      pageMeta.setPage(current, { title: 'B' });
      pageMeta.setPage(gone, null);
      expect(document.title).toBe('B · Play Grifball');
      pageMeta.setPage(current, null);
      expect(document.title).toBe('Match Details · Play Grifball');
    });
  });
});
