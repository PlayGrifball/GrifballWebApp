import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, WritableSignal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AddGamertagHistoryRequest, PastGamertagDto } from '@api/models';
import { SearchBoxComponent } from '@shared/searchBox/searchBox.component';
import { PastGamertagsQuery, PastGamertagsStore } from './pastGamertags.store';

/**
 * Sysadmins: an Xbox account's current gamertag and the gamertags it is known to have gone by, by its XUID or
 * a user's ID (?xboxUserID= or ?userID=, from the gamertag claims page, or typed in here). A manually-added row
 * can be removed again; the rest (e.g. from a roster match) cannot.
 */
@Component({
  selector: 'app-past-gamertags',
  imports: [RouterLink, DatePipe, SearchBoxComponent],
  templateUrl: './pastGamertags.component.html',
  styleUrl: './pastGamertags.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PastGamertagsStore],
})
export class PastGamertagsComponent {
  private readonly store = inject(PastGamertagsStore);

  /** The ?xboxUserID= and ?userID= query parameters: how the page is reached from the claims page or a bookmark. */
  readonly xboxUserID = input<number | null>(null);
  readonly userID = input<number | null>(null);

  /** The lookup fields: seeded from whichever query parameter was given, then whatever is typed here. */
  readonly xuidQuery = signal<number>(0);
  readonly useridQuery = signal<number>(0);

  constructor() {
    this.store.query.use(this.query);
    effect(() => {
      const xboxUserID = Number(this.xboxUserID() ?? 0);
      const userID = Number(this.userID() ?? 0);
      if (xboxUserID)
        this.xuidQuery.set(xboxUserID);
      else if (userID)
        this.useridQuery.set(userID);
    });
  }

  /** What to fetch; undefined (the resource stays idle) until an XUID or user ID is given. The XUID wins if both are. */
  readonly query = computed<PastGamertagsQuery | undefined>(() => {
    if (this.xuidQuery())
      return { xboxUserID: this.xuidQuery() };
    if (this.useridQuery())
      return { userID: this.useridQuery() };
    return undefined;
  });
  readonly hasQuery = computed(() => this.query() !== undefined);

  readonly loading = this.store.loading;
  readonly failed = this.store.failed;
  /** The account (none while loading, or if it could not be loaded). */
  readonly account = this.store.account;
  readonly notFound = this.store.notFound;
  readonly rows = this.store.rows;

  /** The row being removed: its button (and every other Remove button) waits for the answer. */
  readonly removing = this.store.removing;

  remove(row: PastGamertagDto): void {
    this.store.remove(row.xboxGamertagHistoryID);
  }

  // The add-a-gamertag form.
  readonly newGamertag = signal('');
  readonly newFirstSeen = signal('');
  readonly newLastSeen = signal('');
  readonly newNote = signal('');
  readonly adding = this.store.adding;
  readonly addError = this.store.addError;

  /** A plain text input's value, into one of the form fields above. */
  setField(field: WritableSignal<string>, event: Event): void {
    field.set((event.target as HTMLInputElement).value);
  }

  add(): void {
    const account = this.account();
    const gamertag = this.newGamertag().trim();
    if (!account || !gamertag)
      return;
    const dto: AddGamertagHistoryRequest = {
      xboxUserID: account.xboxUserID,
      gamertag,
      firstSeen: toDayStart(this.newFirstSeen()),
      lastSeen: toDayStart(this.newLastSeen()),
      note: this.newNote().trim() || null,
    };
    this.store.add(dto, () => {
      this.newGamertag.set('');
      this.newFirstSeen.set('');
      this.newLastSeen.set('');
      this.newNote.set('');
    });
  }
}

/** A native date input's value ('YYYY-MM-DD') as that day's start, UTC, for the API; empty is null. */
function toDayStart(value: string): string | null {
  return value ? value + 'T00:00:00Z' : null;
}
