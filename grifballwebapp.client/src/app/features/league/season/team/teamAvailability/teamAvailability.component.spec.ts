import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { TeamSlots } from '@api/models';
import { TeamAvailabilityComponent } from './teamAvailability.component';

describe('TeamAvailabilityComponent', () => {
  let fixture: ComponentFixture<TeamAvailabilityComponent>;
  let http: HttpTestingController;
  const slots: TeamSlots = {
    teamID: 7, setByCaptain: false, rosterSize: 5, canChange: false,
    slots: [
      { availabilityOptionID: 1, dayOfWeek: 'Monday', time: '20:00:00', available: true, playersAvailable: 5 },
      { availabilityOptionID: 2, dayOfWeek: 'Tuesday', time: '20:00:00', available: false, playersAvailable: 2 },
    ],
  };

  async function create(canChange: boolean, team: TeamSlots = slots) {
    await TestBed.configureTestingModule({
      imports: [TeamAvailabilityComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamAvailabilityComponent);
    fixture.componentRef.setInput('teamID', 7);
    fixture.detectChanges();
    // Nothing ticked until the team's timeslots arrive
    expect(fixture.componentInstance.ticked()).toHaveSize(0);
    http.expectOne('/api/teams/7/availability').flush({ ...team, canChange });
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const boxes = () => Array.from(el().querySelectorAll('input[type=checkbox]')) as HTMLInputElement[];
  const button = (text: string) => Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes(text)) as HTMLButtonElement | undefined;

  it("shows each timeslot, whether it's the team's and how many of its roster can play it; read-only to others", async () => {
    await create(false);
    const text = el().textContent!;
    expect(text).toContain('Monday 20:00');
    expect(text).toContain('5 of 5 players');
    expect(text).toContain('2 of 5 players');
    expect(text).toContain("From the roster's signups");
    expect(boxes()).toHaveSize(0);
    expect(button('Save')).toBeUndefined();
  });

  it("lets the captain tick the team's own and save them", async () => {
    await create(true);
    expect(boxes().map(b => b.checked)).toEqual([true, false]);
    boxes()[1].click();
    fixture.detectChanges();
    button('Save')!.click();

    expect(http.expectOne({ method: 'POST', url: '/api/teams/7/availability' }).request.body).toEqual({ availabilityOptionIDs: [1, 2] });
  });

  it("lets the captain untick one, and reset the team's own to the roster's", async () => {
    await create(true, { ...slots, setByCaptain: true });
    boxes()[0].click();
    fixture.detectChanges();
    expect(fixture.componentInstance.ticked()).toHaveSize(0);
    expect(button('Save')!.disabled).toBeTrue();

    button("Reset to the roster's")!.click();
    expect(http.expectOne({ method: 'POST', url: '/api/teams/7/availability/reset' }).request.body).toBeNull();
  });
});
