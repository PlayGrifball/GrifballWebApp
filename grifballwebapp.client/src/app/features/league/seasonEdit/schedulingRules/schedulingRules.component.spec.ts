import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SchedulingRulesComponent } from './schedulingRules.component';

describe('SchedulingRulesComponent', () => {
  let fixture: ComponentFixture<SchedulingRulesComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SchedulingRulesComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SchedulingRulesComponent);
    fixture.componentRef.setInput('seasonID', 3);
    fixture.detectChanges();
    http.expectOne('/api/seasons/3/scheduling-rules')
      .flush({ minPlayersAvailable: 4, maxMatchesPerTeamPerNight: 1, maxMatchesPerTeamPerWeek: 2, maxConcurrentMatches: null });
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const input = (name: string) => el().querySelector(`input[name=${name}]`) as HTMLInputElement;

  it("shows the season's rules, and saves them changed; an empty cap is none", async () => {
    expect(input('minPlayersAvailable').value).toBe('4');
    expect(input('maxConcurrentMatches').value).toBe('');

    input('minPlayersAvailable').value = '3';
    input('minPlayersAvailable').dispatchEvent(new Event('input'));
    await fixture.whenStable();
    (Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes('Save')) as HTMLButtonElement).click();

    expect(http.expectOne({ method: 'POST', url: '/api/seasons/3/scheduling-rules' }).request.body)
      .toEqual({ minPlayersAvailable: 3, maxMatchesPerTeamPerNight: 1, maxMatchesPerTeamPerWeek: 2, maxConcurrentMatches: null });
  });
});
