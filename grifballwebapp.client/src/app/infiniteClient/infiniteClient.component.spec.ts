import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InfiniteClientComponent } from './infiniteClient.component';

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

  function init(status: string): void {
    fixture.detectChanges();
    const req = http.expectOne('api/Admin/CheckStatus');
    expect(req.request.responseType).toBe('text');
    req.flush(status);
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;

  it('shows a plain status message', () => {
    init('Token valid');
    expect(component.status).toBe('Token valid');
    expect(el().textContent).toContain('Token valid');
    expect(el().querySelector('a')).toBeNull();
  });

  it('renders a login URL status as a link', () => {
    init('https://login.example/authorize');
    const link = el().querySelector('a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('https://login.example/authorize');
  });

  it('shows a failure status when the check fails', () => {
    spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('api/Admin/CheckStatus').flush('x', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.status).toBe('Failed to check status');
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
    const setCode = Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes('Set Code')) as HTMLButtonElement;
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
    http.expectOne('api/Admin/SetCode?code=xyz').flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');
  });

  it('reports a failure to set the code', () => {
    init('ok');
    spyOn(console, 'log');
    component.code = 'xyz';
    component.setCode();
    http.expectOne('api/Admin/SetCode?code=xyz').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to set code', 'Close');
  });

  it('deletes tokens and reports the result', () => {
    init('ok');
    (Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes('Delete Tokens')) as HTMLButtonElement).click();
    http.expectOne('api/Admin/DeleteTokens').flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');

    spyOn(console, 'log');
    component.deleteTokens();
    http.expectOne('api/Admin/DeleteTokens').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to delete tokens', 'Close');
  });
});
