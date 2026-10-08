import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProfileComponent } from './profile.component';
import { AccountService } from '../account.service';

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let personID: WritableSignal<number | null>;

  beforeEach(async () => {
    personID = signal<number | null>(null);
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: AccountService, useValue: { personID } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '21' } } } },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => http.verify());

  function load(body: { gamertag: string | null } | null): void {
    fixture.detectChanges();
    http.expectOne('api/profile/getgamertag/21').flush(body);
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent!;

  it("shows another user's gamertag read-only", () => {
    personID.set(3);
    load({ gamertag: 'Sgt Grif' });

    expect(component.userID).toBe(21);
    expect(component.settingGamertag).toBeFalse();
    expect(text()).toContain('Gamertag: Sgt Grif');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
  });

  it('does not prompt the owner when the gamertag is already set', () => {
    personID.set(21);
    load({ gamertag: 'Sgt Grif' });
    expect(component.settingGamertag).toBeFalse();
  });

  it('prompts the owner to set a missing gamertag', () => {
    personID.set(21);
    load({ gamertag: null });

    expect(component.settingGamertag).toBeTrue();
    expect(text()).toContain('Please set your gamertag');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).not.toBeNull();
  });

  it('treats a null profile as having no gamertag', () => {
    personID.set(21);
    load(null);
    expect(component.gamertag).toBeNull();
    expect(component.settingGamertag).toBeTrue();
  });

  it('does not prompt other users when the gamertag is missing', () => {
    personID.set(4);
    load({ gamertag: null });
    expect(component.settingGamertag).toBeFalse();
  });

  it('reports a failure to load the gamertag', () => {
    spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('api/profile/getgamertag/21').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed get gamertag', 'Close');
  });

  it('saves the gamertag and leaves edit mode', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag = 'NewTag';
    component.setGamertag();

    http.expectOne('api/profile/setgamertag/21?gamertag=NewTag').flush({});
    fixture.detectChanges();
    expect(component.settingGamertag).toBeFalse();
    expect(text()).toContain('Gamertag: NewTag');
  });

  it('stays in edit mode when saving fails', () => {
    spyOn(console, 'log');
    personID.set(21);
    load({ gamertag: null });
    component.gamertag = 'NewTag';
    component.setGamertag();

    http.expectOne('api/profile/setgamertag/21?gamertag=NewTag').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed set gamertag', 'Close');
    expect(component.settingGamertag).toBeTrue();
  });
});
