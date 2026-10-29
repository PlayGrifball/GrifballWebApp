import { DatePipe } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import { SiteFooterComponent } from './siteFooter.component';

describe('SiteFooterComponent', () => {
  let api: jasmine.SpyObj<ApiClientService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj('ApiClientService', ['commitHash', 'commitDate']);
    api.commitHash.and.returnValue(of('abc123'));
    api.commitDate.and.returnValue(of(new Date()));
    await TestBed.configureTestingModule({
      imports: [SiteFooterComponent],
      providers: [{ provide: ApiClientService, useValue: api }],
    }).compileComponents();
  });

  const create = () => TestBed.createComponent(SiteFooterComponent);
  const text = (f: ComponentFixture<SiteFooterComponent>) => (f.nativeElement as HTMLElement).textContent!;

  it('loads the commit hash and date on creation', () => {
    const date = new Date('2024-01-15T10:30:00Z');
    api.commitHash.and.returnValue(of('test-commit-hash'));
    api.commitDate.and.returnValue(of(date));

    const footer = create().componentInstance;

    expect(footer.commitHash()).toBe('test-commit-hash');
    expect(footer.commitDate()).toEqual(date);
  });

  it('has no build info until the API answers', () => {
    api.commitHash.and.returnValue(new Subject<string>());
    api.commitDate.and.returnValue(new Subject<Date>());

    const footer = create().componentInstance;

    expect(footer.commitHash()).toBeNull();
    expect(footer.commitDate()).toBeNull();
  });

  it('renders the commit hash once it arrives, without a manual detectChanges', async () => {
    const hash = new Subject<string>();
    api.commitHash.and.returnValue(hash);
    const fixture = create();
    fixture.autoDetectChanges();
    await fixture.whenStable();

    hash.next('deadbeef');
    await fixture.whenStable();

    expect(text(fixture)).toContain('deadbeef');
  });

  it('still renders when the build info cannot be loaded', () => {
    api.commitHash.and.returnValue(throwError(() => new Error('down')));
    api.commitDate.and.returnValue(throwError(() => new Error('down')));
    const fixture = create();

    expect(() => fixture.detectChanges()).not.toThrow();
    expect(fixture.componentInstance.commitHash()).toBeNull();
    expect(text(fixture)).toContain('BUILD');
  });

  it('shows the build date as the date pipe shows the API value, linked to the source', () => {
    api.commitDate.and.returnValue(of(new Date('2026-01-02T03:04:05Z')));
    const fixture = create();
    fixture.detectChanges();
    const link = (fixture.nativeElement as HTMLElement).querySelector('a')!;
    expect(link.textContent).toContain(new DatePipe('en-US').transform('2026-01-02T03:04:05Z', 'short')!);
    expect(link.getAttribute('href')).toBe('https://github.com/PlayGrifball/GrifballWebApp');
  });
});
