import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import type { HistoricalProvenanceDto, HistoricalProvenanceKind } from '@api/models';
import { ProvenanceQuery, ProvenanceStore } from './provenance.store';

/** The entity types the import records, as the lookup offers them. */
export const ProvenanceEntities = [
  'User', 'Season', 'MatchmakingSeason', 'Team', 'TeamPlayer', 'PlayerSeasonStat', 'MatchmakingPlayerStat', 'MatchmakingLadderRank',
  'PlayerChampionship', 'SeasonAward', 'SeasonMatch', 'RecordedGame', 'RecordedGameTeam', 'RecordedGameParticipant', 'MatchedMatch',
  'Match', 'GamertagClaim',
] as const;

/** A row's kind in words. */
export function kindLabel(kind: HistoricalProvenanceKind): string {
  switch (kind) {
    case 'Created': return 'Made from';
    case 'Read': return 'Last read from';
    default: return 'Changed by';
  }
}

/**
 * Where the historical import got a row from (sysadmins, on the historical import page): an entity's type and ID give the
 * cells that made it, the ones the latest import read it from and what changed it, each with its sheet, cells and text.
 */
@Component({
  selector: 'app-provenance',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './provenance.component.html',
  styleUrl: './provenance.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ProvenanceStore],
})
export class ProvenanceComponent {
  private readonly store = inject(ProvenanceStore);

  readonly entities = ProvenanceEntities;
  readonly kindLabel = kindLabel;

  /** The lookup's fields: the entity's type and its ID. */
  readonly form = new FormGroup({
    entity: new FormControl<string>(ProvenanceEntities[0], { nonNullable: true }),
    key: new FormControl('', { nonNullable: true }),
  });

  /** The lookup asked; null until one is. */
  readonly query = signal<ProvenanceQuery | null>(null);

  readonly rows = this.store.rows;
  readonly loaded = this.store.loaded;
  readonly failed = this.store.failed;

  constructor() {
    this.store.query.use(this.query);
  }

  /** Looks up the entity the form names; a blank ID asks nothing. */
  lookUp(): void {
    const { entity, key } = this.form.getRawValue();
    const id = key.trim();
    this.query.set(id ? { entity, key: id } : null);
  }

  /** Where a row's cells are: the cells themselves when the source links them, else the workbook. */
  link(row: HistoricalProvenanceDto): string | null {
    return row.cellsUrl ?? row.sourceUrl ?? null;
  }
}
