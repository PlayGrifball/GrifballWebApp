import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlayoffBracketComponent } from './playoffBracket.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { EventEmitter, PLATFORM_ID, signal, WritableSignal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../../account.service';
import { CreateBracketDialogComponent } from './createBracketDialog/createBracketDialog.component';
import { SeedOrderingDialogComponent } from './seedOrderingDialog/seedOrderingDialog.component';
import { of, Subject } from 'rxjs';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('PlayoffBracketComponent', () => {
  let component: PlayoffBracketComponent;
  let fixture: ComponentFixture<PlayoffBracketComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            parent: {
              params: of({ id: 'test-season-id' })
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PlayoffBracketComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

describe('PlayoffBracketComponent behaviour', () => {
  let fixture: ComponentFixture<PlayoffBracketComponent>;
  let component: PlayoffBracketComponent;
  let http: HttpTestingController;
  let dialog: jasmine.SpyObj<MatDialog>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let router: Router;
  let viewer: { addLocale: jasmine.Spy; render: jasmine.Spy; onMatchClicked?: (m: any) => void };
  let originalViewer: unknown;
  let isEventOrganizer: WritableSignal<boolean>;
  let seasonID: string | null;

  const viewerData = (participants: unknown[]) => ({ stages: [], matches: [], matchGames: [], participants } as any);

  beforeEach(async () => {
    seasonID = '6';
    isEventOrganizer = signal(false);
    dialog = jasmine.createSpyObj('MatDialog', ['open']);
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    originalViewer = (window as any).bracketsViewer;
    viewer = { addLocale: jasmine.createSpy('addLocale'), render: jasmine.createSpy('render') };
    (window as any).bracketsViewer = viewer;

    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { isEventOrganizer } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    })
      .overrideProvider(MatDialog, { useValue: dialog })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(PlayoffBracketComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    (window as any).bracketsViewer = originalViewer;
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('does nothing without a season id', () => {
    seasonID = null;
    fixture.detectChanges();
    http.expectNone(() => true);
    expect(text()).toContain('No bracket created');
  });

  it('shows "No bracket created" and skips rendering when the bracket has no participants', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    fixture.detectChanges();

    expect(component.hasBracket()).toBeFalse();
    expect(viewer.render).not.toHaveBeenCalled();
    expect(text()).toContain('No bracket created');
  });

  it('renders the bracket with the custom locale when there are participants', async () => {
    fixture.detectChanges();
    expect(text()).not.toContain('No bracket created'); // not known yet
    const data = viewerData([{ id: 1, name: 'Alpha' }]);
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(data);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.hasBracket()).toBeTrue();
    expect(text()).not.toContain('No bracket created');
    expect(viewer.addLocale).toHaveBeenCalledWith('en', jasmine.objectContaining({
      common: jasmine.objectContaining({ 'group-name-loser-bracket': '{{stage.name}} - Losers' })
    }));
    expect(viewer.render).toHaveBeenCalledWith(data, { clear: true });
    expect(component.drawn()).toBeTrue();
    expect(text()).toContain('BYE = auto-advance');
  });

  it('navigates to the season match page when a bracket match is clicked', async () => {
    const navigate = spyOn(router, 'navigate');
    await component.render(viewerData([{ id: 1 }]));

    viewer.onMatchClicked!({ id: 77 });
    expect(navigate).toHaveBeenCalledWith(['/seasonmatch/77']);
  });

  it('reports a failure to load the bracket', () => {
    spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush('x', { status: 500, statusText: 'Server Error' });

    expect(snackBar.open).toHaveBeenCalledWith('Failed to get viewer data', 'Close');
  });

  it('only shows the bracket controls to organizers', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    expect(text()).not.toContain('Create Bracket');

    isEventOrganizer.set(true);
    fixture.detectChanges();
    expect(text()).toContain('Create Bracket');
    expect(text()).toContain('Set Seeds');
  });

  it('reloads the bracket after one is created in the dialog', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    const created = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: created } } as any);

    component.openDialog();
    expect(dialog.open).toHaveBeenCalledWith(CreateBracketDialogComponent, { data: 6 });

    created.emit();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
  });

  it('reloads the bracket each time it is created again from the same dialog', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    const created = new Subject<void>();
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: created } } as any);
    component.openDialog();

    created.next();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    created.next();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
  });

  it('reloads the bracket after custom seeds are saved', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    const seeded = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { seedingOrder: seeded } } as any);

    component.setSeeds();
    expect(dialog.open).toHaveBeenCalledWith(SeedOrderingDialogComponent, { data: 6, width: '600px', maxHeight: '80vh' });

    seeded.emit();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
  });

  it('stops listening to the dialogs once the page is destroyed', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    const created = new EventEmitter<void>();
    const seeded = new EventEmitter<void>();
    dialog.open.and.returnValues({ componentInstance: { bracketCreated: created } } as any, { componentInstance: { seedingOrder: seeded } } as any);
    component.openDialog();
    component.setSeeds();
    expect(created.observed).toBeTrue();
    expect(seeded.observed).toBeTrue();

    fixture.destroy();

    expect(created.observed).toBeFalse();
    expect(seeded.observed).toBeFalse();
  });

  it('shows "No bracket created" without an error for a season that does not exist (404)', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush('Season does not exist', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(snackBar.open).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No bracket created');
  });

  it('reloads when the bracket is created after its dialog was already closed', () => {
    fixture.detectChanges();
    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
    const created = new Subject<void>();
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: created } } as any);
    component.openDialog();

    // The dialog is gone (Esc), its request still finishes.
    created.next();

    http.expectOne('api/Brackets/GetViewerData?seasonID=6').flush(viewerData([]));
  });
});

describe('PlayoffBracketComponent without the viewer library', () => {
  it('says so when the bracket viewer cannot be loaded', async () => {
    const original = (window as any).bracketsViewer;
    delete (window as any).bracketsViewer;
    const snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { isEventOrganizer: signal(false) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
      ]
    }).overrideProvider(MatSnackBar, { useValue: snackBar }).compileComponents();
    const component = TestBed.createComponent(PlayoffBracketComponent).componentInstance;

    const rendering = component.render({ stages: [], matches: [], matchGames: [], participants: [{ id: 1 }] } as any);
    const script = document.head.querySelector<HTMLScriptElement>('script[src="brackets-viewer.js"]')!;
    script.onerror!(new Event('error'));
    await rendering;

    expect(snackBar.open).toHaveBeenCalledWith('Failed to load the bracket viewer', 'Close');
    (window as any).bracketsViewer = original;
  });
});

describe('PlayoffBracketComponent rendered on the server', () => {
  it('leaves the bracket box for the browser to draw in', async () => {
    const viewer = { addLocale: jasmine.createSpy('addLocale'), render: jasmine.createSpy('render') };
    const original = (window as any).bracketsViewer;
    (window as any).bracketsViewer = viewer;
    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: AccountService, useValue: { isEventOrganizer: signal(false) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
      ]
    }).compileComponents();
    const component = TestBed.createComponent(PlayoffBracketComponent).componentInstance;

    await component.render({ stages: [], matches: [], matchGames: [], participants: [{ id: 1 }] } as any);

    expect(component.hasBracket()).toBeTrue();
    expect(viewer.render).not.toHaveBeenCalled();
    expect(component.drawn()).toBeFalse();
    (window as any).bracketsViewer = original;
  });
});
