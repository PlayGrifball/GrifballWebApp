import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed, fakeAsync, flush, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { MyThemeService } from '@api/services/my-theme.service';
import { UserThemeDto } from '@api/models/user-theme-dto';
import { AccountService } from '../auth/account.service';
import { AppLog } from '../telemetry/appLog';
import { THEME_SAVE_DELAY_MS, THEME_STORAGE_KEY, ThemePersistence, parseThemeSettings } from './themePersistence.service';
import { ThemeSettings, ThemingService } from './theming.service';

const saved: ThemeSettings = {
  era: 'y2k', font: 'Roboto', primary: '#112233', secondary: '#abc', tertiary: '#445566',
  neutral: '#778899', neutralVariant: '#aabbcc', error: '#ff0000',
};

describe('parseThemeSettings', () => {
  it('takes a theme the picker can make', () => {
    expect(parseThemeSettings(saved)).toEqual(saved);
  });

  it('refuses anything else', () => {
    for (const bad of [null, 'x', 5, { ...saved, primary: 'red' }, { ...saved, font: 'Comic Sans' }, { ...saved, era: 'A B' }, { ...saved, error: undefined }])
      expect(parseThemeSettings(bad)).toBeNull();
  });
});

describe('ThemePersistence', () => {
  const personID = signal<number | null>(null);
  let api: jasmine.SpyObj<MyThemeService>;
  let theming: ThemingService;

  function create(platform = 'browser'): ThemePersistence {
    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: platform },
        { provide: AccountService, useValue: { personID, isLoggedIn: () => personID() !== null } },
        { provide: MyThemeService, useValue: api },
        { provide: AppLog, useValue: jasmine.createSpyObj('AppLog', ['warn', 'info', 'error']) },
      ],
    });
    theming = TestBed.inject(ThemingService);
    return TestBed.inject(ThemePersistence);
  }

  beforeEach(() => {
    spyOn(console, 'debug');
    localStorage.clear();
    personID.set(null);
    api = jasmine.createSpyObj('MyThemeService', ['getMyTheme', 'setMyTheme']);
    api.getMyTheme.and.returnValue(of(null as unknown as UserThemeDto));
    api.setMyTheme.and.returnValue(of(undefined));
  });
  afterEach(() => localStorage.clear());

  it('applies the theme stored on this device', () => {
    localStorage.setItem(THEME_STORAGE_KEY, JSON.stringify(saved));
    create();
    expect(theming.settings()).toEqual(saved);
  });

  it('ignores stored junk', () => {
    localStorage.setItem(THEME_STORAGE_KEY, '{"primary":"nope"}');
    const before = create() && theming.settings();
    expect(theming.settings()).toEqual(before);
  });

  it('saves nothing until the theme changes', fakeAsync(() => {
    personID.set(1);
    create();
    TestBed.tick();
    tick(THEME_SAVE_DELAY_MS * 2);
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
    expect(api.setMyTheme).not.toHaveBeenCalled();
  }));

  it('keeps a change on this device when signed out, and does not call the API', fakeAsync(() => {
    create();
    TestBed.tick();
    theming.applySettings(saved);
    TestBed.tick();
    tick(THEME_SAVE_DELAY_MS * 2);
    expect(JSON.parse(localStorage.getItem(THEME_STORAGE_KEY)!)).toEqual(saved);
    expect(api.setMyTheme).not.toHaveBeenCalled();
  }));

  it('saves a change to the account once, after changes stop', fakeAsync(() => {
    personID.set(1);
    create();
    TestBed.tick();
    theming.applySettings({ ...saved, primary: '#000000' });
    TestBed.tick();
    tick(THEME_SAVE_DELAY_MS / 2);
    theming.applySettings(saved);
    TestBed.tick();
    tick(THEME_SAVE_DELAY_MS);
    expect(api.setMyTheme).toHaveBeenCalledOnceWith({ body: saved });
  }));

  it('lets the account\'s theme win on sign-in, keeping it on this device', fakeAsync(() => {
    api.getMyTheme.and.returnValue(of(saved));
    personID.set(1);
    const persistence = create();
    TestBed.tick();
    flush();
    expect(theming.settings()).toEqual(saved);
    expect(JSON.parse(localStorage.getItem(THEME_STORAGE_KEY)!)).toEqual(saved);
    expect(persistence.restored()).toBe(1);
    expect(api.setMyTheme).not.toHaveBeenCalled();
  }));

  it('sends this device\'s theme up when the account has none', fakeAsync(() => {
    localStorage.setItem(THEME_STORAGE_KEY, JSON.stringify(saved));
    personID.set(1);
    create();
    TestBed.tick();
    flush();
    expect(api.setMyTheme).toHaveBeenCalledOnceWith({ body: saved });
  }));

  it('does nothing on the server render', () => {
    localStorage.setItem(THEME_STORAGE_KEY, JSON.stringify(saved));
    const persistence = create('server');
    TestBed.tick();
    expect(persistence.restored()).toBe(0);
    expect(theming.settings()).not.toEqual(saved);
  });
});
