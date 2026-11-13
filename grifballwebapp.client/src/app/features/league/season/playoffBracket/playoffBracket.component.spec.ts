import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlayoffBracketComponent } from './playoffBracket.component';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { EventEmitter, PLATFORM_ID, signal, WritableSignal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../../../../core/auth/account.service';
import { CreateBracketDialogComponent } from './createBracketDialog/createBracketDialog.component';
import { SeedOrderingDialogComponent } from './seedOrderingDialog/seedOrderingDialog.component';
import { Subject } from 'rxjs';

describe('PlayoffBracketComponent', () => {
  let component: PlayoffBracketComponent;
  let fixture: ComponentFixture<PlayoffBracketComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
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
  let viewer: { addLocale: jasmine.Spy; render: jasmine.Spy };
  let originalViewer: unknown;
  let isEventOrganizer: WritableSignal<boolean>;

  const viewerData = (participants: unknown[]) => ({ stages: [], matches: [], matchGames: [], participants } as any);

  beforeEach(async () => {
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
      ]
    })
      .overrideProvider(MatDialog, { useValue: dialog })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(PlayoffBracketComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', 6);
  });

  afterEach(() => {
    http.verify();
    (window as any).bracketsViewer = originalViewer;
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('does nothing without a season id', () => {
    fixture.componentRef.setInput('seasonID', 0);
    fixture.detectChanges();
    http.expectNone(() => true);
    expect(text()).toContain('No bracket created');
  });

  it('shows "No bracket created" and skips rendering when the bracket has no participants', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    fixture.detectChanges();

    expect(component.hasBracket()).toBeFalse();
    expect(viewer.render).not.toHaveBeenCalled();
    expect(text()).toContain('No bracket created');
  });

  it('renders the bracket with the custom locale when there are participants', async () => {
    fixture.detectChanges();
    expect(text()).not.toContain('No bracket created'); // not known yet
    const data = viewerData([{ id: 1, name: 'Alpha' }]);
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(data);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.hasBracket()).toBeTrue();
    expect(text()).not.toContain('No bracket created');
    expect(viewer.addLocale).toHaveBeenCalledWith('en', jasmine.objectContaining({
      common: jasmine.objectContaining({ 'group-name-loser-bracket': '{{stage.name}} - Losers' })
    }));
    expect(viewer.render).toHaveBeenCalledWith(data, jasmine.objectContaining({ clear: true }));
    expect(component.drawn()).toBeTrue();
    expect(text()).toContain('BYE = auto-advance');
  });

  it("loads the new season's bracket when the season changes, showing none of the previous one meanwhile", async () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([{ id: 1, name: 'Alpha' }]));
    await fixture.whenStable();
    expect(component.drawn()).toBeTrue();

    fixture.componentRef.setInput('seasonID', 7);
    fixture.detectChanges();
    expect(component.hasBracket()).toBeFalse();
    expect(component.drawn()).toBeFalse();
    expect(component.loaded()).toBeFalse();

    const data = viewerData([{ id: 2, name: 'Bravo' }]);
    http.expectOne('/api/seasons/7/bracket/viewer-data').flush(data);
    await fixture.whenStable();
    expect(viewer.render).toHaveBeenCalledWith(data, jasmine.objectContaining({ clear: true }));
    expect(component.hasBracket()).toBeTrue();
  });

  it("drops the previous season's bracket when it arrives late", () => {
    fixture.detectChanges();
    const late = http.expectOne('/api/seasons/6/bracket/viewer-data');
    fixture.componentRef.setInput('seasonID', 7);
    fixture.detectChanges();

    http.expectOne('/api/seasons/7/bracket/viewer-data').flush(viewerData([]));
    late.flush(viewerData([{ id: 1, name: 'Alpha' }]));

    expect(component.hasBracket()).toBeFalse();
    expect(viewer.render).not.toHaveBeenCalled();
  });

  it('opens the dialogs for the season shown', () => {
    fixture.componentRef.setInput('seasonID', 7);
    fixture.detectChanges();
    http.expectOne('/api/seasons/7/bracket/viewer-data').flush(viewerData([]));
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: new Subject<void>(), seedingOrder: new Subject<void>() } } as any);

    component.openDialog();
    component.setSeeds();

    expect(dialog.open.calls.argsFor(0)[1]).toEqual(jasmine.objectContaining({ data: 7 }));
    expect(dialog.open.calls.argsFor(1)[1]).toEqual(jasmine.objectContaining({ data: 7 }));
  });

  it('navigates to the season match page when a bracket match is clicked', async () => {
    const navigate = spyOn(router, 'navigate');
    await component.render(viewerData([{ id: 1 }]));

    // The click handler goes in render's config (onMatchClick)
    viewer.render.calls.mostRecent().args[1].onMatchClick({ id: 77 });
    expect(navigate).toHaveBeenCalledWith(['/seasonmatch/77']);
  });

  it('reports a failure to load the bracket', () => {
    spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush('x', { status: 500, statusText: 'Server Error' });

    expect(snackBar.open).toHaveBeenCalledWith('Failed to get viewer data', 'Close');
  });

  it('only shows the bracket controls to organizers', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    expect(text()).not.toContain('Create Bracket');

    isEventOrganizer.set(true);
    fixture.detectChanges();
    expect(text()).toContain('Create Bracket');
    expect(text()).toContain('Set Seeds');
  });

  it('reloads the bracket after one is created in the dialog', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    const created = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: created } } as any);

    let changed = 0;
    component.bracketChanged.subscribe(() => changed++);

    component.openDialog();
    expect(dialog.open).toHaveBeenCalledWith(CreateBracketDialogComponent, { data: 6 });

    created.emit();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    expect(changed).toBe(1); // the page fetches the champion again: the server cleared it
  });

  it('reloads the bracket each time it is created again from the same dialog', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    const created = new Subject<void>();
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: created } } as any);
    let changed = 0;
    component.bracketChanged.subscribe(() => changed++);
    component.openDialog();

    created.next();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    created.next();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    expect(changed).toBe(2);
    expect(dialog.open).toHaveBeenCalledTimes(1);
  });

  it('reloads the bracket after custom seeds are saved', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    const seeded = new EventEmitter<void>();
    dialog.open.and.returnValue({ componentInstance: { seedingOrder: seeded } } as any);

    let changed = 0;
    component.bracketChanged.subscribe(() => changed++);

    component.setSeeds();
    expect(dialog.open).toHaveBeenCalledWith(SeedOrderingDialogComponent, { data: 6, width: '600px', maxHeight: '80vh' });

    seeded.emit();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    expect(changed).toBe(1);
  });

  it('stops listening to the dialogs once the page is destroyed', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
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
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush({ status: 404, title: 'Not found', detail: 'Season does not exist' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(snackBar.open).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No bracket created');
  });

  describe('champion slot', () => {
    const slot = () => (fixture.nativeElement as HTMLElement).querySelector('.bracket-champion');
    const champion = (tier: string, teamID: number, teamName: string) => ({ tier, teamID, teamName, roster: [] });

    async function drawBracket(): Promise<void> {
      fixture.detectChanges();
      http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([{ id: 1, name: 'Alpha' }]));
      await fixture.whenStable();
      fixture.detectChanges();
    }

    it('names the champion team, linked to its team page', async () => {
      fixture.componentRef.setInput('champions', [champion('Open', 41, 'Relentless')]);
      await drawBracket();

      expect(slot()!.textContent).toContain('Champion');
      const link = slot()!.querySelector('a')!;
      expect(link.textContent).toBe('Relentless');
      expect(link.getAttribute('href')).toBe('/season/6/team/41');
      expect(slot()!.querySelector('.bracket-champion__tier')).toBeNull();
      expect(slot()!.textContent).not.toContain('To be decided');
    });

    it('names each tier when the season has several champions', async () => {
      fixture.componentRef.setInput('champions', [champion('Pro', 41, 'Relentless'), champion('Legacy', 42, 'Bashers')]);
      await drawBracket();

      const tiers = Array.from(slot()!.querySelectorAll('.bracket-champion__tier')).map(e => e.textContent);
      expect(tiers).toEqual(['Pro', 'Legacy']);
    });

    it('says the champion is to be decided while the bracket has none', async () => {
      fixture.componentRef.setInput('champions', []);
      await drawBracket();

      expect(slot()!.textContent).toContain('To be decided');
      expect(slot()!.querySelector('a')).toBeNull();
    });

    it('is left out for an ended season that has no champion, rather than "to be decided"', async () => {
      fixture.componentRef.setInput('champions', []);
      fixture.componentRef.setInput('hasEnded', true);
      await drawBracket();

      expect(slot()).toBeNull();
      expect(text()).not.toContain('To be decided');
    });

    it('names the champion of an ended season', async () => {
      fixture.componentRef.setInput('champions', [champion('Open', 41, 'Relentless')]);
      fixture.componentRef.setInput('hasEnded', true);
      await drawBracket();

      expect(slot()!.querySelector('a')!.textContent).toBe('Relentless');
    });

    it('is left out when no champions are given', async () => {
      await drawBracket();
      expect(slot()).toBeNull();
    });

    it('is left out without a bracket', () => {
      fixture.componentRef.setInput('champions', [champion('Open', 41, 'Relentless')]);
      fixture.detectChanges();
      http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
      fixture.detectChanges();

      expect(slot()).toBeNull();
    });
  });

  it('reloads when the bracket is created after its dialog was already closed', () => {
    fixture.detectChanges();
    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    const created = new Subject<void>();
    dialog.open.and.returnValue({ componentInstance: { bracketCreated: created } } as any);
    component.openDialog();

    let changed = 0;
    component.bracketChanged.subscribe(() => changed++);

    // The dialog is gone (Esc), its request still finishes.
    created.next();

    http.expectOne('/api/seasons/6/bracket/viewer-data').flush(viewerData([]));
    expect(changed).toBe(1);
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
