import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, WritableSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { GamertagClaimsService } from '@api/services/gamertag-claims.service';
import type { AddGamertagHistoryRequest, PastGamertagDto } from '@api/models';
import { SearchBoxComponent } from '../../shared/searchBox/searchBox.component';
import { problemText } from '../../shared/errorText';
import { nullWhenNotFound } from '../../core/http/nullWhenNotFound';

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
})
export class PastGamertagsComponent {
  private api = inject(GamertagClaimsService);
  private snackBar = inject(MatSnackBar);

  /** The ?xboxUserID= and ?userID= query parameters: how the page is reached from the claims page or a bookmark. */
  readonly xboxUserID = input<number | null>(null);
  readonly userID = input<number | null>(null);

  /** The lookup fields: seeded from whichever query parameter was given, then whatever is typed here. */
  readonly xuidQuery = signal<number>(0);
  readonly useridQuery = signal<number>(0);

  constructor() {
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
  readonly query = computed<{ xboxUserID?: number, userID?: number } | undefined>(() => {
    if (this.xuidQuery())
      return { xboxUserID: this.xuidQuery() };
    if (this.useridQuery())
      return { userID: this.useridQuery() };
    return undefined;
  });
  readonly hasQuery = computed(() => this.query() !== undefined);

  readonly past = rxResource({
    params: () => this.query(),
    // 404: no such Xbox account (by that XUID or user ID).
    stream: ({ params }) => this.api.getPastGamertags(params!).pipe(nullWhenNotFound()),
  });

  readonly loading = computed(() => this.past.isLoading());
  readonly failed = computed(() => this.past.status() === 'error');
  /** The account (none while loading, or if it could not be loaded). */
  readonly account = computed(() => this.past.hasValue() ? this.past.value() : null);
  readonly notFound = computed(() => this.past.status() === 'resolved' && this.account() === null);
  readonly rows = computed<PastGamertagDto[]>(() => this.account()?.pastGamertags ?? []);

  /** The row being removed: its button (and every other Remove button) waits for the answer. */
  readonly removing = signal<number | null>(null);

  remove(row: PastGamertagDto): void {
    this.removing.set(row.xboxGamertagHistoryID);
    this.api.removeGamertagHistory({ historyID: row.xboxGamertagHistoryID }).subscribe({
      next: () => {
        this.removing.set(null);
        this.past.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.removing.set(null);
        this.snackBar.open(problemText(error, "Couldn't remove that gamertag."), 'Close');
      },
    });
  }

  // The add-a-gamertag form.
  readonly newGamertag = signal('');
  readonly newFirstSeen = signal('');
  readonly newLastSeen = signal('');
  readonly newNote = signal('');
  readonly adding = signal(false);
  readonly addError = signal<string | null>(null);

  /** A plain text input's value, into one of the form fields above. */
  setField(field: WritableSignal<string>, event: Event): void {
    field.set((event.target as HTMLInputElement).value);
  }

  add(): void {
    const account = this.account();
    const gamertag = this.newGamertag().trim();
    if (!account || !gamertag)
      return;
    this.adding.set(true);
    this.addError.set(null);
    const dto: AddGamertagHistoryRequest = {
      xboxUserID: account.xboxUserID,
      gamertag,
      firstSeen: toDayStart(this.newFirstSeen()),
      lastSeen: toDayStart(this.newLastSeen()),
      note: this.newNote().trim() || null,
    };
    this.api.addGamertagHistory({ body: dto }).subscribe({
      next: () => {
        this.adding.set(false);
        this.newGamertag.set('');
        this.newFirstSeen.set('');
        this.newLastSeen.set('');
        this.newNote.set('');
        this.past.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.adding.set(false);
        this.addError.set(problemText(error, "Couldn't add that gamertag."));
      },
    });
  }
}

/** A native date input's value ('YYYY-MM-DD') as that day's start, UTC, for the API; empty is null. */
function toDayStart(value: string): string | null {
  return value ? value + 'T00:00:00Z' : null;
}
