import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import type { LinkVodRequest, VodReviewDto } from '@api/models';
import { VodReviewStore } from './vodReview.store';

/** What the matcher suggested for a VOD, "Spring League 2024 · Alpha, Bravo · its match", or null for nothing. */
export function suggestion(vod: VodReviewDto): string | null {
  if (vod.seasonID == null)
    return null;
  return [vod.seasonName, vod.teams.map(t => t.teamName).join(', '), vod.seasonMatchID != null ? 'their match' : null]
    .filter(part => !!part).join(' · ');
}

/**
 * The VODs the matcher wasn't sure of (sysadmins, on the historical import page): accept its suggestion, link one to a
 * season, its match and teams, or dismiss it for good. The refresh that finds them is the import's own.
 */
@Component({
  selector: 'app-vod-review',
  imports: [DatePipe, MatCheckboxModule],
  templateUrl: './vodReview.component.html',
  styleUrl: './vodReview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [VodReviewStore],
})
export class VodReviewComponent {
  private readonly store = inject(VodReviewStore);

  readonly suggestion = suggestion;

  readonly rows = this.store.rows;
  readonly loaded = this.store.loaded;
  readonly failed = this.store.failed;

  /** The VOD being linked by hand, and what it is being linked to. */
  readonly editing = signal<VodReviewDto | null>(null);
  readonly seasonID = signal<number | null>(null);
  readonly seasonMatchID = signal<number | null>(null);
  readonly teamIDs = signal<number[]>([]);

  /** Loaded with the first editor opened. */
  readonly seasonRows = this.store.seasonRows;
  readonly optionsValue = this.store.optionsValue;

  /** An action waiting for its answer: the buttons wait too. */
  readonly busy = this.store.busy;

  constructor() {
    this.store.editing.use(computed(() => this.editing() !== null));
    this.store.seasonID.use(this.seasonID);
  }

  watchUrl(vod: VodReviewDto): string {
    return 'https://www.youtube.com/watch?v=' + encodeURIComponent(vod.youTubeVideoID);
  }

  accept(vod: VodReviewDto): void {
    this.link(vod, { seasonID: vod.seasonID ?? null, seasonMatchID: vod.seasonMatchID ?? null, teamIDs: vod.teams.map(t => t.teamID) });
  }

  edit(vod: VodReviewDto): void {
    this.editing.set(vod);
    this.seasonID.set(vod.seasonID ?? null);
    this.seasonMatchID.set(vod.seasonMatchID ?? null);
    this.teamIDs.set(vod.teams.map(t => t.teamID));
  }

  cancel(): void {
    this.editing.set(null);
  }

  chooseSeason(value: string): void {
    this.seasonID.set(value ? Number(value) : null);
    this.seasonMatchID.set(null);
    this.teamIDs.set([]);
  }

  /** A match brings its two teams. */
  chooseMatch(value: string): void {
    const id = value ? Number(value) : null;
    this.seasonMatchID.set(id);
    const match = this.optionsValue()?.matches.find(m => m.seasonMatchID === id);
    if (match)
      this.teamIDs.set([...match.teamIDs]);
  }

  toggleTeam(teamID: number, checked: boolean): void {
    this.teamIDs.update(ids => checked ? [...ids.filter(id => id !== teamID), teamID] : ids.filter(id => id !== teamID));
  }

  save(): void {
    const vod = this.editing();
    if (vod && this.seasonID() != null)
      this.link(vod, { seasonID: this.seasonID(), seasonMatchID: this.seasonMatchID(), teamIDs: this.teamIDs() });
  }

  dismiss(vod: VodReviewDto): void {
    this.store.dismiss(vod.vodID, () => this.editing.set(null));
  }

  private link(vod: VodReviewDto, request: LinkVodRequest): void {
    this.store.link(vod.vodID, request, () => this.editing.set(null));
  }
}
