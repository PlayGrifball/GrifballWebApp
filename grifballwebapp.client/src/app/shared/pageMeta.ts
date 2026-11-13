import { DOCUMENT } from '@angular/common';
import { DestroyRef, effect, inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { DateTime } from 'luxon';

export const SITE_NAME = 'Play Grifball';
export const DEFAULT_DESCRIPTION = 'Halo Infinite Grifball seasons, teams, schedules, brackets and match stats.';
/**
 * The public site: every page's canonical address and og:url, whichever host served it (www, test, a preview), so
 * search engines list the public page and link previews point to it.
 */
export const PUBLIC_ORIGIN = 'https://grifball.xyz';
/** The league's time zone (ScheduleService.LeagueTimeZoneId on the server): the day a match or season is on. */
export const LEAGUE_TIME_ZONE = 'America/New_York';

/** What a page says about itself in search results and link previews. */
export interface PageDescription {
  title: string;
  description?: string;
  /** The page's canonical path when it isn't the one it was reached by (/season/0 is the current season's). */
  path?: string;
}

/** The tab's title: the page's, then the site's; the site's alone for a page without one (the home page). */
export function fullTitle(title: string | undefined): string {
  return title ? `${title} · ${SITE_NAME}` : SITE_NAME;
}

/** A count in a description: "1 season", "3 seasons". */
export function plural(count: number, one: string, many = one + 's'): string {
  return `${count} ${count === 1 ? one : many}`;
}

/**
 * A date in a description: the league's day (US Eastern, so an evening match isn't on the next day as in UTC), in
 * English. The same on the server and in every visitor's browser, so the page's tags don't change when the browser
 * takes it over.
 */
export function pageDate(date: DateTime | null): string | undefined {
  return date?.isValid ? date.setZone(LEAGUE_TIME_ZONE).setLocale('en-US').toFormat('LLL d, yyyy') : undefined;
}

/** The page's own address, as search engines should list it: no query or fragment. */
export function canonicalUrl(origin: string, routerUrl: string): string {
  const path = routerUrl.split(/[?#]/)[0] || '/';
  return origin + (path.startsWith('/') ? path : '/' + path);
}

/**
 * The page's title, description, link-preview tags (Open Graph, Twitter) and canonical URL. Every navigation sets
 * the route's (PageMetaTitleStrategy); a page with data describes itself once its data is in (describePage), on the
 * server before the HTML is sent, so crawlers and link previews read the page's own. A page's description wins over
 * its route's whichever comes first, and goes when the page does.
 */
@Injectable({ providedIn: 'root' })
export class PageMeta {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  private routeTitle: string | undefined;
  private url = '/';
  private page: { owner: object, description: PageDescription } | null = null;

  /** The route's title (none: the site's alone) and address, on each navigation. */
  setRoute(title: string | undefined, routerUrl: string): void {
    this.routeTitle = title;
    this.url = routerUrl;
    this.apply();
  }

  /** A page's own description (null while it has none), until it clears it. The same description again changes nothing. */
  setPage(owner: object, description: PageDescription | null): void {
    if (description) {
      if (this.page?.owner === owner && sameDescription(this.page.description, description))
        return;
      this.page = { owner, description };
    }
    else if (this.page?.owner === owner)
      this.page = null;
    else
      return;
    this.apply();
  }

  private apply(): void {
    const page = this.page?.description;
    const title = fullTitle(page ? page.title : this.routeTitle);
    const description = page?.description ?? DEFAULT_DESCRIPTION;
    const url = canonicalUrl(PUBLIC_ORIGIN, page?.path ?? this.url);
    this.title.setTitle(title);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ name: 'twitter:title', content: title });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.canonicalLink().setAttribute('href', url);
  }

  private canonicalLink(): HTMLLinkElement {
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.document.head.appendChild(link);
    }
    return link;
  }
}

function sameDescription(a: PageDescription, b: PageDescription): boolean {
  return a.title === b.title && a.description === b.description && a.path === b.path;
}

/** The route's title and address for PageMeta on every navigation, in place of Angular's (title only). */
@Injectable({ providedIn: 'root' })
export class PageMetaTitleStrategy extends TitleStrategy {
  private readonly pageMeta = inject(PageMeta);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.pageMeta.setRoute(this.buildTitle(snapshot), snapshot.url);
  }
}

/**
 * Describes the page from its data, in a component's injection context: the description follows the source (null
 * while there is none: the route's stands) and goes with the component.
 */
export function describePage(source: () => PageDescription | null): void {
  const pageMeta = inject(PageMeta);
  const owner = {};
  effect(() => pageMeta.setPage(owner, source()));
  inject(DestroyRef).onDestroy(() => pageMeta.setPage(owner, null));
}
