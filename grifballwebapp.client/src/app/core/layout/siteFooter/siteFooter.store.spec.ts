import { TestBed } from '@angular/core/testing';
import { BuildService } from '@api/services/build.service';
import { of, throwError } from 'rxjs';
import { SiteFooterStore } from './siteFooter.store';

describe('SiteFooterStore', () => {
  let api: jasmine.SpyObj<BuildService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<BuildService>('BuildService', ['getCommitHash', 'getCommitDate']);
    TestBed.configureTestingModule({ providers: [SiteFooterStore, { provide: BuildService, useValue: api }] });
  });

  it('reads the commit and its date', () => {
    const date = new Date('2024-01-15T10:30:00Z');
    api.getCommitHash.and.returnValue(of('abc123'));
    api.getCommitDate.and.returnValue(of(date.toISOString()));
    const store = TestBed.inject(SiteFooterStore);
    expect(store.commitHash()).toBe('abc123');
    expect(store.commitDate()).toEqual(date);
  });

  it('has neither when they cannot be read, and no date for an empty one', () => {
    api.getCommitHash.and.returnValue(throwError(() => new Error('down')));
    api.getCommitDate.and.returnValue(of(''));
    const store = TestBed.inject(SiteFooterStore);
    expect(store.commitHash()).toBeNull();
    expect(store.commitDate()).toBeNull();
  });
});
