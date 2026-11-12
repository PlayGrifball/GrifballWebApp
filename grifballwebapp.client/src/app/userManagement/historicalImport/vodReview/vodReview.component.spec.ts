import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, Subject, throwError } from 'rxjs';
import { ApiClientService } from '../../../api/apiClient.service';
import { VodReviewDto } from '@api/models';
import { suggestion, VodReviewComponent } from './vodReview.component';

function review(id: number, changes: Partial<VodReviewDto> = {}): VodReviewDto {
  return {
    vodID: id, youTubeVideoID: 'Hb2JmxJhQd8', title: `VOD ${id}`, publishedAt: '2026-10-16T00:00:00Z', durationSeconds: 6601,
    seasonID: 5, seasonName: 'Summer League 2026', seasonMatchID: null, teams: [{ teamID: 31, teamName: 'Low IQ' }],
    description: null, publishedAtIsApproximate: true, matchStatus: 'NeedsReview', matchNote: 'Only Low IQ found in Summer League 2026', ...changes,
  };
}

describe('VodReviewComponent', () => {
  let fixture: ComponentFixture<VodReviewComponent>;
  let api: jasmine.SpyObj<ApiClientService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  const el = () => fixture.nativeElement as HTMLElement;
  const button = (label: string) => el().querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;
  const byText = (text: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === text)!;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function create(rows: VodReviewDto[]): Promise<void> {
    api.getVodReview.and.returnValue(of(rows));
    fixture = TestBed.createComponent(VodReviewComponent);
    fixture.detectChanges();
    await settle();
  }

  beforeEach(() => {
    api = jasmine.createSpyObj('ApiClientService', ['getVodReview', 'getVodLinkSeasons', 'getVodLinkOptions', 'linkVod', 'dismissVod']);
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    api.linkVod.and.returnValue(of(undefined));
    api.dismissVod.and.returnValue(of(undefined));
    api.getVodLinkSeasons.and.returnValue(of([{ seasonID: 5, seasonName: 'Summer League 2026' }, { seasonID: 6, seasonName: 'Fall League 2026' }]));
    api.getVodLinkOptions.and.callFake((seasonID: number) => of(seasonID === 6
      ? { teams: [], matches: [] }
      : {
          teams: [{ teamID: 31, teamName: 'Low IQ' }, { teamID: 32, teamName: 'Reach for the Balls' }],
          matches: [{ seasonMatchID: 90, label: 'Low IQ vs Reach for the Balls · Oct 3, 2026', teamIDs: [31, 32] }],
        }));
    TestBed.configureTestingModule({
      imports: [VodReviewComponent],
      providers: [{ provide: ApiClientService, useValue: api }, { provide: MatSnackBar, useValue: snackBar }],
    });
  });

  it('says when there is nothing to review', async () => {
    await create([]);
    expect(el().textContent).toContain('Nothing to review.');
  });

  it('says when the list could not be loaded', async () => {
    api.getVodReview.and.returnValue(throwError(() => new Error('down')));
    fixture = TestBed.createComponent(VodReviewComponent);
    fixture.detectChanges();
    await settle();
    expect(el().querySelector('[role=alert]')!.textContent).toContain("couldn't be loaded");
  });

  it('lists each VOD with its link, date, suggestion and note', async () => {
    await create([review(1), review(2, { seasonID: null, seasonName: null, teams: [], matchNote: null, publishedAtIsApproximate: false })]);

    const items = el().querySelectorAll('.vr-item');
    expect(items).toHaveSize(2);
    expect(items[0].querySelector<HTMLAnchorElement>('.vr-title')!.href).toBe('https://www.youtube.com/watch?v=Hb2JmxJhQd8');
    expect(items[0].textContent).toContain('(about)');
    expect(items[0].textContent).toContain('Suggested: Summer League 2026 · Low IQ');
    expect(items[0].textContent).toContain('Only Low IQ found');
    expect(items[1].textContent).toContain('Suggested: nothing');
    expect(items[1].textContent).not.toContain('(about)');
    expect(button('Accept the suggestion for VOD 2')).withContext('Nothing to accept').toBeNull();
  });

  it("accepts the matcher's suggestion", async () => {
    await create([review(1, { seasonMatchID: 90, teams: [{ teamID: 31, teamName: 'Low IQ' }, { teamID: 32, teamName: 'Reach for the Balls' }] })]);
    api.getVodReview.and.returnValue(of([]));

    button('Accept the suggestion for VOD 1').click();
    await settle();

    expect(api.linkVod).toHaveBeenCalledWith(1, { seasonID: 5, seasonMatchID: 90, teamIDs: [31, 32] });
    expect(el().textContent).toContain('Nothing to review.');
  });

  it('dismisses a VOD', async () => {
    await create([review(1)]);
    api.getVodReview.and.returnValue(of([]));

    button('Dismiss VOD 1').click();
    await settle();

    expect(api.dismissVod).toHaveBeenCalledWith(1);
    expect(el().textContent).toContain('Nothing to review.');
  });

  it('links a VOD by hand: a season, its match (which brings its teams) and teams', async () => {
    await create([review(1)]);

    button('Link VOD 1').click();
    await settle();
    expect(api.getVodLinkSeasons).toHaveBeenCalled();
    const season = el().querySelector<HTMLSelectElement>('#vr-season')!;
    expect(el().querySelector('label[for="vr-season"]')).not.toBeNull();
    expect(season.selectedOptions[0].textContent!.trim()).withContext('The suggestion preselected').toBe('Summer League 2026');
    const boxes = () => Array.from(el().querySelectorAll<HTMLInputElement>('.vr-team input'));
    expect(boxes().map(b => b.checked)).toEqual([true, false]);

    const match = el().querySelector<HTMLSelectElement>('#vr-match')!;
    match.value = '90';
    match.dispatchEvent(new Event('change'));
    await settle();
    expect(boxes().map(b => b.checked)).toEqual([true, true]);

    boxes()[0].click();
    await settle();
    byText('Save link').click();
    await settle();

    expect(api.linkVod).toHaveBeenCalledWith(1, { seasonID: 5, seasonMatchID: 90, teamIDs: [32] });
    expect(el().querySelector('.vr-editor')).toBeNull();
  });

  it('starts over when another season is chosen, and cancels', async () => {
    await create([review(1, { seasonID: null, seasonName: null, teams: [] })]);

    button('Link VOD 1').click();
    await settle();
    expect(byText('Save link').disabled).toBeTrue();
    expect(el().querySelector('#vr-match')).withContext('No season yet').toBeNull();

    const season = el().querySelector<HTMLSelectElement>('#vr-season')!;
    season.value = '6';
    season.dispatchEvent(new Event('change'));
    await settle();
    expect(api.getVodLinkOptions).toHaveBeenCalledWith(6);
    expect(el().querySelector('.vr-teams')!.textContent).toContain('This season has no teams.');
    expect(byText('Save link').disabled).toBeFalse();

    season.value = '';
    season.dispatchEvent(new Event('change'));
    await settle();
    expect(fixture.componentInstance.seasonID()).toBeNull();
    fixture.componentInstance.save();
    expect(api.linkVod).not.toHaveBeenCalled();

    byText('Cancel').click();
    await settle();
    expect(el().querySelector('.vr-editor')).toBeNull();
  });

  it('says why a decision failed, and keeps the VOD', async () => {
    await create([review(1)]);
    const failure = new Subject<void>();
    api.dismissVod.and.returnValue(failure);

    button('Dismiss VOD 1').click();
    await settle();
    expect(button('Dismiss VOD 1').disabled).toBeTrue();
    failure.error(new HttpErrorResponse({ status: 404, error: 'gone' }));
    await settle();

    expect(snackBar.open).toHaveBeenCalled();
    expect(button('Dismiss VOD 1').disabled).toBeFalse();
    expect(el().querySelectorAll('.vr-item')).toHaveSize(1);
  });

  it('describes a suggestion', () => {
    expect(suggestion(review(1, { seasonMatchID: 9 }))).toBe('Summer League 2026 · Low IQ · their match');
    expect(suggestion(review(1, { teams: [] }))).toBe('Summer League 2026');
    expect(suggestion(review(1, { seasonID: null }))).toBeNull();
  });
});
