import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import type { ServiceRecordDto, ServiceRecordTotalsDto } from '@api/models';

export type ServiceRecordScope = 'all' | 'league' | 'matchmaking';

/** The stat strip at the top of a profile: K/D, MMR and bomb scores, over league, Late League or both. */
@Component({
  selector: 'app-service-record',
  imports: [DecimalPipe],
  templateUrl: './serviceRecord.component.html',
  styleUrl: './serviceRecord.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceRecordComponent {
  readonly record = input<ServiceRecordDto | null | undefined>(null);

  readonly scopes: { value: ServiceRecordScope; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'league', label: 'League' },
    { value: 'matchmaking', label: 'Late League' },
  ];
  readonly scope = signal<ServiceRecordScope>('all');

  readonly totals = computed<ServiceRecordTotalsDto | null>(() => this.record()?.[this.scope()] ?? null);

  selectScope(scope: ServiceRecordScope): void {
    this.scope.set(scope);
  }
}
