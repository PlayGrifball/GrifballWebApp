import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { provideRouter, TitleStrategy } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DateTime } from 'luxon';
import { canonicalUrl, DEFAULT_DESCRIPTION, describePage, fullTitle, pageDate, PageDescription, PageMeta, PageMetaTitleStrategy, PUBLIC_ORIGIN, SITE_NAME } from './pageMeta';

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

  it('titles a page after itself, then the site; an untitled one after the site alone', () => {
    expect(fullTitle('Leaderboards')).toBe('Leaderboards · Play Grifball');
    expect(fullTitle('Home')).toBe('Home · Play Grifball');
    expect(fullTitle(undefined)).toBe(SITE_NAME);
  });

  it('gives the page\'s address without its query or fragment', () => {
    expect(canonicalUrl('https://grifball.xyz', '/match/abc?tab=medals#x')).toBe('https://grifball.xyz/match/abc');
    expect(canonicalUrl('https://grifball.xyz', '/')).toBe('https://grifball.xyz/');
    expect(canonicalUrl('https://grifball.xyz', '')).toBe('https://grifball.xyz/');
    expect(canonicalUrl('https://grifball.xyz', '?x=1')).toBe('https://grifball.xyz/');
  });

  it('writes a date the same everywhere: the league\'s day (US Eastern), in English', () => {
    // 9:00 pm Eastern on May 23: the league's evening, not the next day in UTC
    expect(pageDate(DateTime.fromISO('2024-05-24T01:00:02Z'))).toBe('May 23, 2024');
    expect(pageDate(DateTime.fromISO('2024-05-24T05:00:00Z'))).toBe('May 24, 2024');
    expect(pageDate(DateTime.fromISO('2026-11-20T01:00:00Z'))).toBe('Nov 19, 2026');
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
            { path: 'untitled', component: PlainPage },
          ]),
          { provide: TitleStrategy, useClass: PageMetaTitleStrategy },
        ],
      });
    });

    beforeEach(async () => {
      router = await RouterTestingHarness.create();
    });

    it('sets the route\'s title, the default description and the page\'s public address on each navigation', async () => {
      await router.navigateByUrl('/seasons?page=2');
      // The public site's, whichever host served it
      const url = 'https://grifball.xyz/seasons';
      expect(PUBLIC_ORIGIN).toBe('https://grifball.xyz');
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

    it('titles a route without a title after the site alone', async () => {
      await router.navigateByUrl('/untitled');
      expect(document.title).toBe(SITE_NAME);
      expect(meta('property="og:title"')).toBe(SITE_NAME);
    });

    it('takes the address a page gives itself, as its canonical one', async () => {
      await router.navigateByUrl('/match/0');
      described.set({ title: 'Current', path: '/match/12' });
      TestBed.tick();
      expect(canonical()).toBe('https://grifball.xyz/match/12');
      expect(meta('property="og:url"')).toBe('https://grifball.xyz/match/12');

      described.set(null);
      TestBed.tick();
      expect(canonical()).toBe('https://grifball.xyz/match/0');
    });

    it('doesn\'t write the tags again for the same description', async () => {
      await router.navigateByUrl('/match/abc');
      const updateTag = spyOn(TestBed.inject(Meta), 'updateTag').and.callThrough();
      described.set({ title: 'Red 5–3 Blue', description: 'Red beat Blue 5–3.' });
      TestBed.tick();
      const writes = updateTag.calls.count();
      expect(writes).toBeGreaterThan(0);

      described.set({ title: 'Red 5–3 Blue', description: 'Red beat Blue 5–3.' });
      TestBed.tick();
      expect(updateTag.calls.count()).toBe(writes);
    });

    it('lets a page describe itself once its data is in, and drops that when it goes', async () => {
      await router.navigateByUrl('/match/abc');
      expect(document.title).toBe('Match Details · Play Grifball');

      described.set({ title: 'Red 5–3 Blue', description: 'Red beat Blue 5–3.' });
      TestBed.tick();
      expect(document.title).toBe('Red 5–3 Blue · Play Grifball');
      expect(meta('name="description"')).toBe('Red beat Blue 5–3.');
      expect(meta('property="og:title"')).toBe('Red 5–3 Blue · Play Grifball');
      expect(canonical()).toBe('https://grifball.xyz/match/abc');

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
      expect(canonical()).toBe('https://grifball.xyz/seasons');
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
