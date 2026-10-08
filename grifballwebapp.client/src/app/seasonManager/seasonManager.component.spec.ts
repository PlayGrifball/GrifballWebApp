import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { SeasonManagerComponent } from './seasonManager.component';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';

describe('SeasonManagerComponent', () => {
  let fixture: ComponentFixture<SeasonManagerComponent>;
  let api: jasmine.SpyObj<ApiClientService>;

  const season = (seasonID: number, seasonName: string): SeasonDto =>
    Object.assign(new SeasonDto(), { seasonID, seasonName, signupsCount: seasonID * 10, signupsOpen: '2026-01-02T03:04:00' });

  beforeEach(async () => {
    api = jasmine.createSpyObj('ApiClientService', ['getSeasons']);
    await TestBed.configureTestingModule({
      imports: [SeasonManagerComponent],
      providers: [provideRouter([]), { provide: ApiClientService, useValue: api }]
    }).compileComponents();
    fixture = TestBed.createComponent(SeasonManagerComponent);
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists each season with its signup count and an edit link', () => {
    api.getSeasons.and.returnValue(of([season(1, 'Season 1'), season(2, 'Season 2')]));
    fixture.detectChanges();

    const rows = Array.from(el().querySelectorAll('tr.mat-mdc-row'));
    expect(rows.length).toBe(2);
    const cells = Array.from(rows[1].querySelectorAll('td')).map(td => td.textContent!.trim());
    expect(cells[0]).toBe('Season 2');
    expect(cells[1]).toMatch(/^1\/2\/26, 3:04\sAM$/);
    expect(cells[6]).toBe('20');
    expect(rows[1].querySelector('a')!.getAttribute('href')).toBe('/seasonEdit/2');
  });

  it('links "Create New Season" to a new season edit page', () => {
    api.getSeasons.and.returnValue(of([]));
    fixture.detectChanges();
    const create = fixture.debugElement.query(By.css('button'));
    expect(create.nativeElement.textContent).toContain('Create New Season');
    expect(create.injector.get(RouterLink).urlTree!.toString()).toBe('/seasonEdit/0');
  });

  it('logs and shows no rows when loading fails', () => {
    const err = spyOn(console, 'error');
    api.getSeasons.and.returnValue(throwError(() => 'boom'));
    fixture.detectChanges();

    expect(err).toHaveBeenCalledWith('boom');
    expect(el().querySelectorAll('tr.mat-mdc-row').length).toBe(0);
  });
});
