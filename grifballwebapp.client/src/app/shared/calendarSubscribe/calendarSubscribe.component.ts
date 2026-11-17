import { ChangeDetectionStrategy, Component, DOCUMENT, computed, inject, input, linkedSignal } from '@angular/core';
import { Clipboard } from '@angular/cdk/clipboard';

/** A calendar feed's address on this site: the API's own path (`teams/5/calendar.ics`) under `/api`. */
export function calendarFeedUrl(origin: string, path: string): string {
  return `${origin}/api/${path.replace(/^\/+/, '')}`;
}

/** The same address as webcal://, which opens a calendar app to subscribe to it. */
export function webcalUrl(url: string): string {
  return url.replace(/^https?:\/\//i, 'webcal://');
}

/**
 * "Subscribe to calendar": a feed of matches (iCalendar) for Google Calendar, Apple Calendar or Outlook, as a webcal://
 * link that opens the calendar app and an address to copy for one that adds a calendar from a URL. The feed is public:
 * a calendar app fetches it signed in to nothing.
 */
@Component({
  selector: 'app-calendar-subscribe',
  templateUrl: './calendarSubscribe.component.html',
  styleUrl: './calendarSubscribe.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CalendarSubscribeComponent {
  private readonly document = inject(DOCUMENT);
  private readonly clipboard = inject(Clipboard);

  /** The feed's path on the API, as the server routes it: `teams/5/calendar.ics`. */
  readonly path = input.required<string>();
  /** What the feed holds, for the text beside it: "This team's matches". */
  readonly description = input('Matches');

  readonly url = computed(() => calendarFeedUrl(this.document.location.origin, this.path()));
  readonly webcal = computed(() => webcalUrl(this.url()));
  /** The address is on the clipboard: forgotten for another (the page kept for another team or player). */
  readonly copied = linkedSignal({ source: this.url, computation: () => false });

  copy(): void {
    this.copied.set(this.clipboard.copy(this.url()));
  }
}
