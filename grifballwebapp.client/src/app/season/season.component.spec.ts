import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { RouterTestingHarness } from '@angular/router/testing';
import { SeasonComponent } from './season.component';
import { ApiClientService } from '../api/apiClient.service';
import { ScheduleListComponent } from './scheduleList/scheduleList.component';
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';

@Component({ selector: 'app-schedule-list', template: '' })
class StubScheduleList {}
@Component({ selector: 'app-playoff-bracket', template: '' })
class StubPlayoffBracket {}
@Component({ selector: 'app-team-standings', template: '' })
class StubTeamStandings {}

describe('SeasonComponent', () => {
  let fixture: ComponentFixture<SeasonComponent>;
  let api: jasmine.SpyObj<ApiClientService>;
  let seasonID: string | null;

  beforeEach(async () => {
    seasonID = '5';
    api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'getSeasonName']);
    api.getSeasonName.and.callFake((id: number) => of(`Season ${id}`));
    api.getCurrentSeasonID.and.returnValue(of(8));

    await TestBed.configureTestingModule({
      imports: [SeasonComponent],
      providers: [
        provideRouter([]),
        { provide: ApiClientService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    })
      .overrideComponent(SeasonComponent, {
        remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent] },
        add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(SeasonComponent);
  });

  const heading = () => (fixture.nativeElement as HTMLElement).querySelector('h2')!.textContent;

  it('shows the name of the season in the route', () => {
    fixture.detectChanges();

    expect(api.getSeasonName).toHaveBeenCalledWith(5);
    expect(api.getCurrentSeasonID).not.toHaveBeenCalled();
    expect(heading()).toBe('Season 5');
  });

  it('falls back to the current season when the route has none', () => {
    seasonID = null;
    fixture.detectChanges();

    expect(api.getCurrentSeasonID).toHaveBeenCalled();
    expect(api.getSeasonName).toHaveBeenCalledWith(8);
    expect(heading()).toBe('Season 8');
  });

});

describe('SeasonComponent links', () => {
  it('links to the sub-pages of the current season', async () => {
    const api = jasmine.createSpyObj('ApiClientService', ['getCurrentSeasonID', 'getSeasonName']);
    api.getSeasonName.and.returnValue(of('Season 5'));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'season/:seasonID', component: SeasonComponent }]),
        { provide: ApiClientService, useValue: api },
      ]
    }).overrideComponent(SeasonComponent, {
      remove: { imports: [ScheduleListComponent, PlayoffBracketComponent, TeamStandingsComponent] },
      add: { imports: [StubScheduleList, StubPlayoffBracket, StubTeamStandings] }
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/season/5', SeasonComponent);

    const hrefs = Array.from(harness.routeNativeElement!.querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(['/season/5/signups', '/season/5/signupForm', '/season/5/teams', '/season/5/playergrades']);
  });
});
