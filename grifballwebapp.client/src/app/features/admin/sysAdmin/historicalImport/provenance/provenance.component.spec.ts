import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { HistoricalProvenanceDto } from '@api/models';
import { kindLabel, ProvenanceComponent } from './provenance.component';
import { ProvenanceStore } from './provenance.store';

const row = (over: Partial<HistoricalProvenanceDto> = {}): HistoricalProvenanceDto => ({
  historicalProvenanceID: 1, entity: 'User', entityKey: '4182', kind: 'Created', recordedAt: '2026-11-28T12:00:00Z',
  historicalImportJobID: 12, historicalSourceSnapshotID: 34, sourceID: 'wl23', sourceTitle: 'Winter League 2023',
  sourceUrl: 'https://docs.google.com/spreadsheets/d/abc/edit', sheet: 'Stats', cells: 'A5:K5', cellsUrl: null,
  value: 'Rayzyr | Tuna', detail: 'Placeholder for Rayzyr', ...over,
}) as HistoricalProvenanceDto;

describe('ProvenanceComponent', () => {
  let fixture: ComponentFixture<ProvenanceComponent>;
  const rows = signal<HistoricalProvenanceDto[]>([]);
  const loaded = signal(false);
  const failed = signal(false);

  beforeEach(() => {
    rows.set([]);
    loaded.set(false);
    failed.set(false);
    // The lookup asked is the component's own (its query signal): the store's answer is these signals
    const store = { query: { use: () => undefined }, rows, loaded, failed };
    TestBed.overrideComponent(ProvenanceComponent, { set: { providers: [{ provide: ProvenanceStore, useValue: store }] } });
    fixture = TestBed.createComponent(ProvenanceComponent);
    fixture.detectChanges();
  });

  const el = (): HTMLElement => fixture.nativeElement;

  it('looks up the entity asked, and nothing for a blank ID', () => {
    fixture.componentInstance.lookUp('Team', '  ');
    expect(fixture.componentInstance.query()).toBeNull();

    (el().querySelector('#pv-entity') as HTMLSelectElement).value = 'Team';
    (el().querySelector('#pv-key') as HTMLInputElement).value = ' 7 ';
    (el().querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(fixture.componentInstance.query()).toEqual({ entity: 'Team', key: '7' });
  });

  it("shows each row's source, tab, cells, text and why, linked to the workbook", () => {
    fixture.componentInstance.lookUp('User', '4182');
    rows.set([row(), row({ historicalProvenanceID: 2, kind: 'Read', cellsUrl: 'https://docs.google.com/spreadsheets/d/abc/edit#gid=1&range=A9', cells: 'A9', detail: null })]);
    loaded.set(true);
    fixture.detectChanges();

    const items = el().querySelectorAll('.pv-item');
    expect(items).toHaveSize(2);
    expect(items[0].textContent).toContain('Made from');
    expect(items[0].textContent).toContain('Stats');
    expect(items[0].textContent).toContain('A5:K5');
    expect(items[0].textContent).toContain('Placeholder for Rayzyr');
    expect(items[0].querySelector('a')?.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/abc/edit');
    expect(items[1].querySelector('a')?.getAttribute('href')).withContext('the cells themselves when the source links them').toContain('range=A9');
  });

  it('says when nothing is recorded, and when the lookup fails', () => {
    fixture.componentInstance.lookUp('Team', '7');
    loaded.set(true);
    fixture.detectChanges();
    expect(el().querySelector('.hud-empty')?.textContent).toContain('Nothing recorded for Team 7');

    failed.set(true);
    fixture.detectChanges();
    expect(el().querySelector('[role=alert]')).not.toBeNull();
  });

  it('names each kind of row', () => {
    expect([kindLabel('Created'), kindLabel('Read'), kindLabel('Changed')]).toEqual(['Made from', 'Last read from', 'Changed by']);
    expect(fixture.componentInstance.link(row({ sourceUrl: null }))).toBeNull();
  });
});
