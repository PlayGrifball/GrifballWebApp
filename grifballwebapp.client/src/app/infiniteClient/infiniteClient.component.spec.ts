import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HaloStatus, InfiniteClientComponent } from './infiniteClient.component';

describe('InfiniteClientComponent', () => {
  let fixture: ComponentFixture<InfiniteClientComponent>;
  let component: InfiniteClientComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let codeParam: string | null;

  beforeEach(async () => {
    codeParam = null;
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [InfiniteClientComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => codeParam } } } },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InfiniteClientComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const button = (text: string) =>
    Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes(text)) as HTMLButtonElement | undefined;

  const gatewayStatus = (overrides: Partial<HaloStatus> = {}): HaloStatus => ({
    mode: 'Gateway',
    gatewayUrl: 'http://halo-gateway.halo-gateway.svc:8080/',
    gatewayAdminUrl: 'https://grunt.grifball.xyz/admin',
    apiKeyConfigured: true,
    reachable: true,
    accountStatus: 'Healthy',
    apiCode: 200,
    healthy: true,
    message: 'Halo gateway account Healthy; test call: Halo Infinite API answered 200',
    ...overrides,
  });

  describe('in Direct mode', () => {
    /** The server says Direct, then answers CheckStatus with status. */
    function init(status: string): void {
      fixture.detectChanges();
      http.expectOne('api/Admin/HaloStatus').flush({ mode: 'Direct' });
      const req = http.expectOne('api/Admin/CheckStatus');
      expect(req.request.responseType).toBe('text');
      req.flush(status);
      fixture.detectChanges();
    }

    it('says which mode is active', () => {
      init('Good');
      expect(component.mode()).toBe('Direct');
      expect(el().querySelector('.ic-mode')!.textContent).toContain('Direct');
    });

    it('shows a plain status message', () => {
      init('Token valid');
      expect(component.status()).toBe('Token valid');
      expect(el().textContent).toContain('Token valid');
      expect(el().querySelector('[role="status"] a')).toBeNull();
    });

    it('renders a login URL status as a link', () => {
      init('https://login.example/authorize');
      const link = el().querySelector('[role="status"] a') as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('https://login.example/authorize');
    });

    it('shows a failure status when the check fails', () => {
      spyOn(console, 'log');
      fixture.detectChanges();
      http.expectOne('api/Admin/HaloStatus').flush({ mode: 'Direct' });
      http.expectOne('api/Admin/CheckStatus').flush('x', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(component.status()).toBe('Failed to check status');
      expect(snackBar.open).toHaveBeenCalledWith('Failed to check status', 'Close');
    });

    it('leaves the code empty when there is no code query parameter', () => {
      init('ok');
      expect(component.code).toBe('');
    });

    it('pre-fills the code from the redirect query parameter', () => {
      codeParam = 'abc123';
      init('ok');
      expect(component.code).toBe('abc123');
    });

    it('only enables Set Code once a code is entered', async () => {
      init('ok');
      await fixture.whenStable();
      fixture.detectChanges();
      const setCode = button('Set Code')!;
      expect(setCode.disabled).toBeTrue();

      const input = el().querySelector('input[name="code"]') as HTMLInputElement;
      input.value = 'xyz';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(setCode.disabled).toBeFalse();
    });

    it('sends the code and reports success', () => {
      init('ok');
      component.code = 'xyz';
      component.setCode();
      http.expectOne({ method: 'POST', url: 'api/Admin/SetCode?code=xyz' }).flush({});
      expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');
    });

    it('URL-encodes a code with a space, &, and +', () => {
      init('ok');
      component.code = 'a b&c+d';
      component.setCode();
      http.expectOne({ method: 'POST', url: 'api/Admin/SetCode?code=a%20b%26c%2Bd' }).flush({});
      expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');
    });

    it('reports a failure to set the code', () => {
      init('ok');
      spyOn(console, 'log');
      component.code = 'xyz';
      component.setCode();
      http.expectOne({ method: 'POST', url: 'api/Admin/SetCode?code=xyz' }).flush('x', { status: 400, statusText: 'Bad Request' });
      expect(snackBar.open).toHaveBeenCalledWith('Failed to set code', 'Close');
    });

    it('deletes tokens and reports the result', () => {
      init('ok');
      button('Delete Tokens')!.click();
      http.expectOne({ method: 'POST', url: 'api/Admin/DeleteTokens' }).flush({});
      expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');

      spyOn(console, 'log');
      component.deleteTokens();
      http.expectOne({ method: 'POST', url: 'api/Admin/DeleteTokens' }).flush('x', { status: 500, statusText: 'Server Error' });
      expect(snackBar.open).toHaveBeenCalledWith('Failed to delete tokens', 'Close');
    });

    it('shows no gateway status', () => {
      init('Good');
      expect(el().querySelector('.ic-gateway')).toBeNull();
      expect(el().textContent).not.toContain('signed in centrally');
    });

    it('shows the status once it arrives, without a manual detectChanges', async () => {
      await fixture.whenStable();
      http.expectOne('api/Admin/HaloStatus').flush({ mode: 'Direct' });
      http.expectOne('api/Admin/CheckStatus').flush('Connected');
      await fixture.whenStable();

      expect(el().textContent).toContain('Connected');
    });
  });

  describe('in Gateway mode', () => {
    function init(status: HaloStatus = gatewayStatus()): void {
      fixture.detectChanges();
      http.expectOne('api/Admin/HaloStatus').flush(status);
      fixture.detectChanges();
    }

    it('says which mode is active and never asks for the direct status', () => {
      init();
      expect(component.mode()).toBe('Gateway');
      expect(el().querySelector('.ic-mode')!.textContent).toContain('Halo gateway');
      http.expectNone('api/Admin/CheckStatus');
    });

    it('explains that the account is signed in at the gateway, with a link to its admin page', () => {
      init();
      expect(el().textContent).toContain('The Halo account is signed in centrally at the Halo gateway');
      const link = Array.from(el().querySelectorAll('a')).find(a => a.textContent!.includes('admin page')) as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('https://grunt.grifball.xyz/admin');
    });

    it('falls back to the default admin page link when the server gives none', () => {
      init(gatewayStatus({ gatewayAdminUrl: null }));
      expect(component.gatewayAdminUrl()).toBe('https://grunt.grifball.xyz/admin');
    });

    it('hides the direct sign-in controls', () => {
      init();
      expect(button('Set Code')).toBeUndefined();
      expect(button('Delete Tokens')).toBeUndefined();
      expect(el().querySelector('input[name="code"]')).toBeNull();
      expect(el().textContent).toContain('Direct');
    });

    it('shows a working gateway', () => {
      init();
      const status = el().querySelector('.ic-gateway')!;
      expect(status.textContent).toContain('Working');
      expect(status.textContent).toContain('http://halo-gateway.halo-gateway.svc:8080/');
      expect(status.textContent).toContain('Healthy');
      expect(status.textContent).toContain('200');
      expect(status.querySelector('.ic-good')).not.toBeNull();
    });

    it('shows an unreachable gateway', () => {
      init(gatewayStatus({
        reachable: false, accountStatus: null, apiCode: null, healthy: false,
        message: 'Halo gateway unreachable (HttpRequestException); test call: Halo Infinite client failed: HttpRequestException',
      }));
      const status = el().querySelector('.ic-gateway')!;
      expect(status.textContent).toContain('Not working');
      expect(status.textContent).toContain('No answer');
      expect(status.textContent).toContain('Unknown');
      expect(status.textContent).toContain('Halo gateway unreachable');
      expect(status.querySelector('.ic-bad')).not.toBeNull();
    });

    it('shows a gateway whose account needs signing in again, and a missing API key', () => {
      init(gatewayStatus({ accountStatus: 'Degraded', healthy: false, apiKeyConfigured: false, apiCode: 401 }));
      const status = el().querySelector('.ic-gateway')!;
      expect(status.textContent).toContain('Degraded');
      expect(status.textContent).toContain('401');
      expect(status.textContent).toContain('Not set (HaloGateway:ApiKey)');
    });

    it('checks again on request', () => {
      init(gatewayStatus({ healthy: false, accountStatus: 'Degraded' }));
      button('Check again')!.click();
      fixture.detectChanges();
      expect(button('Checking')!.disabled).toBeTrue();
      http.expectOne('api/Admin/HaloStatus').flush(gatewayStatus());
      fixture.detectChanges();

      expect(component.gateway()!.healthy).toBeTrue();
      expect(el().querySelector('.ic-gateway')!.textContent).toContain('Working');
      http.expectNone('api/Admin/CheckStatus');
    });

    it('reports a failed re-check and keeps the last status', () => {
      init();
      spyOn(console, 'log');
      component.loadStatus();
      http.expectOne('api/Admin/HaloStatus').flush('x', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(snackBar.open).toHaveBeenCalledWith('Failed to check the Halo gateway', 'Close');
      expect(component.mode()).toBe('Gateway');
      expect(component.gateway()!.healthy).toBeTrue();
      http.expectNone('api/Admin/CheckStatus');
    });
  });

  it('falls back to the direct page when the server cannot say its mode', () => {
    spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('api/Admin/HaloStatus').flush('x', { status: 404, statusText: 'Not Found' });
    http.expectOne('api/Admin/CheckStatus').flush('Good');
    fixture.detectChanges();

    expect(component.mode()).toBe('Direct');
    expect(el().textContent).toContain('Good');
    expect(button('Set Code')).toBeDefined();
  });

  it('shows that it is checking before the server answers', () => {
    fixture.detectChanges();
    expect(el().querySelector('.ic-mode')!.textContent).toContain('Checking');
    http.expectOne('api/Admin/HaloStatus').flush({ mode: 'Direct' });
    http.expectOne('api/Admin/CheckStatus').flush('Good');
  });
});
