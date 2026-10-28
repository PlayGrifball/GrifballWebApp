import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Location } from '@angular/common';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BROWSER_NAVIGATE, LinkedAccountsComponent, LinkedLogins } from './linkedAccounts.component';

describe('LinkedAccountsComponent', () => {
  let fixture: ComponentFixture<LinkedAccountsComponent>;
  let component: LinkedAccountsComponent;
  let http: HttpTestingController;
  let navigate: jasmine.Spy<(url: string) => void>;
  let location: jasmine.SpyObj<Location>;
  let queryParams: Record<string, string>;

  const discord = { loginProvider: 'Discord', providerKey: '111', providerDisplayName: 'Discord' };
  const providers = [{ name: 'Discord', displayName: 'Discord' }];
  const mine = (over: Partial<LinkedLogins> = {}): LinkedLogins => ({ hasPassword: true, logins: [discord], providers, ...over });

  const el = () => fixture.nativeElement as HTMLElement;
  const buttons = () => Array.from(el().querySelectorAll('button')) as HTMLButtonElement[];
  const button = (label: string) => buttons().find(b => b.textContent!.trim() === label);
  const status = () => el().querySelector('[role="status"]')?.textContent?.trim();

  function create(userID?: number): void {
    TestBed.configureTestingModule({
      imports: [LinkedAccountsComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
        { provide: Location, useValue: location },
        { provide: BROWSER_NAVIGATE, useValue: navigate },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LinkedAccountsComponent);
    component = fixture.componentInstance;
    if (userID !== undefined)
      fixture.componentRef.setInput('userID', userID);
    fixture.detectChanges();
  }

  function flushMine(body: LinkedLogins): void {
    http.expectOne('/api/ExternalLogins/Mine').flush(body);
    fixture.detectChanges();
  }

  beforeEach(() => {
    queryParams = {};
    navigate = jasmine.createSpy('navigate');
    location = jasmine.createSpyObj<Location>('Location', ['path', 'replaceState']);
    location.path.and.returnValue('/profile/21?linkCallback=true');
  });

  afterEach(() => http.verify());

  it('lists your logins with unlink, and a link button per provider', () => {
    create();
    flushMine(mine());

    expect(el().querySelector('.linked-provider')!.textContent).toBe('Discord');
    expect(el().querySelector('.linked-key')!.textContent).toBe('111');
    expect(button('Unlink')).toBeDefined();
    expect(button('Link Discord')).toBeDefined();
    expect(el().querySelector('.linked-hint')).toBeNull();
  });

  it('never offers to unlink your only way to sign in', () => {
    create();
    flushMine(mine({ hasPassword: false }));

    expect(button('Unlink')).toBeUndefined();
    expect(el().querySelector('.linked-hint')!.textContent).toContain('only way to sign in');
  });

  it('offers unlink without a password while another login remains', () => {
    create();
    flushMine(mine({ hasPassword: false, logins: [discord, { ...discord, providerKey: '222' }] }));

    expect(buttons().filter(b => b.textContent!.trim() === 'Unlink').length).toBe(2);
  });

  it('says so when nothing is linked', () => {
    create();
    flushMine(mine({ logins: [] }));
    expect(el().textContent).toContain('No linked accounts.');
  });

  it('starts a link and sends the browser to the provider with the token', () => {
    create();
    flushMine(mine());

    button('Link Discord')!.click();
    const req = http.expectOne('/api/ExternalLogins/StartLink?provider=Discord');
    expect(req.request.method).toBe('POST');
    fixture.detectChanges();
    expect(button('Link Discord')!.disabled).toBeTrue();
    req.flush({ token: 'a+b/c' });

    expect(navigate).toHaveBeenCalledWith('/api/ExternalLogins/Link?provider=Discord&token=a%2Bb%2Fc');
  });

  it('reports a link that cannot start', () => {
    create();
    flushMine(mine());

    component.link(providers[0]);
    http.expectOne('/api/ExternalLogins/StartLink?provider=Discord').flush("Unknown login provider 'Discord'", { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(status()).toBe("Unknown login provider 'Discord'");
    expect(component.busy()).toBeFalse();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('unlinks and reloads', () => {
    create();
    flushMine(mine());

    button('Unlink')!.click();
    const req = http.expectOne('/api/ExternalLogins/Unlink');
    expect(req.request.body).toEqual({ loginProvider: 'Discord', providerKey: '111' });
    req.flush(null);
    flushMine(mine({ logins: [] }));

    expect(status()).toBe('Unlinked Discord');
    expect(el().textContent).toContain('No linked accounts.');
  });

  it("shows the server's reason when unlinking is refused", () => {
    create();
    flushMine(mine());

    component.unlink(discord);
    http.expectOne('/api/ExternalLogins/Unlink').flush('This is your only way to sign in.', { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(status()).toBe('This is your only way to sign in.');
    expect(component.busy()).toBeFalse();
  });

  it('falls back to a generic message without one from the server', () => {
    create();
    http.expectOne('/api/ExternalLogins/Mine').flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(status()).toBe('Failed to load linked accounts');
  });

  it('finishes a link on return from the provider and clears the query parameter', () => {
    queryParams = { linkCallback: 'true' };
    create();

    expect(location.replaceState).toHaveBeenCalledWith('/profile/21');
    const req = http.expectOne('/api/ExternalLogins/LinkCallback');
    expect(req.request.method).toBe('POST');
    req.flush(discord);
    flushMine(mine());

    expect(status()).toBe('Linked Discord');
  });

  it('explains a login already linked to another account', () => {
    queryParams = { linkCallback: 'true' };
    create();

    http.expectOne('/api/ExternalLogins/LinkCallback').flush(
      'This Discord account is already linked to another Grifball account. Ask an admin to merge the two accounts.',
      { status: 409, statusText: 'Conflict' });
    flushMine(mine());

    expect(status()).toContain('Ask an admin to merge');
  });

  describe('for a sysadmin', () => {
    it("lists the user's logins with remove and no link buttons", () => {
      create(15);
      http.expectOne('/api/ExternalLogins/UserLogins/15').flush({ hasPassword: false, logins: [discord, { ...discord, providerKey: '222' }] });
      fixture.detectChanges();

      expect(buttons().map(b => b.textContent!.trim())).toEqual(['Remove', 'Remove']);
      expect(el().querySelector('.linked-providers')).toBeNull();
    });

    it('ignores a link callback parameter', () => {
      queryParams = { linkCallback: 'true' };
      create(15);
      http.expectOne('/api/ExternalLogins/UserLogins/15').flush({ hasPassword: true, logins: [] });
      expect(location.replaceState).not.toHaveBeenCalled();
    });

    it("removes one of the user's logins", () => {
      create(15);
      http.expectOne('/api/ExternalLogins/UserLogins/15').flush({ hasPassword: true, logins: [discord] });

      component.unlink(discord);
      http.expectOne('/api/ExternalLogins/RemoveUserLogin/15').flush(null);
      http.expectOne('/api/ExternalLogins/UserLogins/15').flush({ hasPassword: true, logins: [] });
      fixture.detectChanges();

      expect(status()).toBe('Unlinked Discord');
    });
  });
});

describe('BROWSER_NAVIGATE', () => {
  it('defaults to a full-page navigation', () => {
    expect(typeof TestBed.inject(BROWSER_NAVIGATE)).toBe('function');
  });
});
