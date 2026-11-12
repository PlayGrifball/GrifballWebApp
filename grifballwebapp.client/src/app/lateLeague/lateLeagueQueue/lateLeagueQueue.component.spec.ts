import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { HttpTransportType, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { AuthSession } from '../../authSession.service';
import type { QueueStatusDto } from '@api/models';
import { QUEUE_HUB_URL, LateLeagueQueueComponent } from './lateLeagueQueue.component';

describe('LateLeagueQueueComponent', () => {
  let connection: {
    start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy; onreconnected: jasmine.Spy; onclose: jasmine.Spy;
    state: HubConnectionState;
  };
  let withUrl: jasmine.Spy;
  let fixture: ComponentFixture<LateLeagueQueueComponent>;
  let http: HttpTestingController;

  const anonymous: QueueStatusDto = { count: 3, open: true, signedIn: false, hasGamertag: false, inQueue: false, inMatch: false };
  const player: QueueStatusDto = { count: 3, open: true, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');
  const button = () => el().querySelector<HTMLButtonElement>('button');
  const statusRequest = () => http.expectOne('/api/queue');
  /** Lets the connection's promise callbacks run. */
  const settled = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
  const handlerFor = (event: string) => connection.on.calls.all().find(c => c.args[0] === event)!.args[1] as (value: unknown) => void;

  function create(platform = 'browser'): void {
    TestBed.configureTestingModule({
      imports: [LateLeagueQueueComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: PLATFORM_ID, useValue: platform }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LateLeagueQueueComponent);
    fixture.detectChanges();
  }

  function show(status: QueueStatusDto): void {
    statusRequest().flush(status);
    fixture.detectChanges();
  }

  beforeEach(() => {
    connection = {
      // Never connects, unless a spec says otherwise: a connection loads the status again
      start: jasmine.createSpy('start').and.returnValue(new Promise(() => {})),
      stop: jasmine.createSpy('stop').and.returnValue(Promise.resolve()),
      on: jasmine.createSpy('on'),
      onreconnected: jasmine.createSpy('onreconnected'),
      onclose: jasmine.createSpy('onclose'),
      state: HubConnectionState.Disconnected,
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as any);
  });

  afterEach(() => http.verify());

  it('calls itself Late League, and links to the Late League page unless told not to', () => {
    create();
    show(anonymous);
    expect(el().querySelector('h2')!.textContent!.trim()).toBe('Late League queue');
    expect(el().querySelector('a[href="/late-league"]')!.textContent!.trim()).toBe('Late League games and stats');
    fixture.componentRef.setInput('showLink', false);
    fixture.detectChanges();
    expect(el().querySelector('a[href="/late-league"]')).toBeNull();
  });

  it('connects to the queue hub over a WebSocket and loads the status', async () => {
    create();
    expect(withUrl.calls.mostRecent().args[0]).toBe(QUEUE_HUB_URL);
    expect(QUEUE_HUB_URL).toBe('hub/QueueHub');
    expect(withUrl.calls.mostRecent().args[1]).toEqual({ skipNegotiation: true, transport: HttpTransportType.WebSockets });
    await settled();
    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(text()).toContain('Loading the Late League queue');
    show(anonymous);
    expect(text()).toContain('3 players in the queue');
  });

  it('stops the connection when the page goes', () => {
    create();
    show(anonymous);
    fixture.destroy();
    expect(connection.stop).toHaveBeenCalledTimes(1);
  });

  it('does nothing on the server: no request, no connection', () => {
    create('server');
    expect(connection.start).not.toHaveBeenCalled();
    expect(text()).toContain('Loading the Late League queue');
  });

  it('asks visitors to sign in, and says one player in the singular', () => {
    create();
    show({ ...anonymous, count: 1 });
    expect(text()).toContain('1 player in the queue');
    expect(el().querySelector('a')!.getAttribute('href')).toBe('/login');
    expect(button()).toBeNull();
  });

  it('tells a player without a gamertag to set one on Discord', () => {
    create();
    show({ ...player, hasGamertag: false });
    expect(text()).toContain('Set your gamertag');
    expect(button()).toBeNull();
  });

  it('offers nothing to a player in a match', () => {
    create();
    show({ ...player, inMatch: true });
    expect(text()).toContain('You are in a Late League match');
    expect(button()).toBeNull();
  });

  it('joins the queue, then offers to leave it', () => {
    create();
    show(player);
    expect(button()!.textContent).toContain('Join the queue');

    button()!.click();
    fixture.detectChanges();
    expect(button()!.disabled).toBeTrue();
    const join = http.expectOne('/api/queue/join');
    expect(join.request.method).toBe('POST');
    join.flush({ ...player, count: 4, inQueue: true });
    fixture.detectChanges();

    expect(text()).toContain('4 players in the queue');
    expect(text()).toContain('You have joined the Late League queue');
    expect(button()!.textContent).toContain('Leave the queue');
    expect(button()!.disabled).toBeFalse();
  });

  it('leaves the queue', () => {
    create();
    show({ ...player, inQueue: true });

    button()!.click();
    const leave = http.expectOne('/api/queue/leave');
    expect(leave.request.method).toBe('POST');
    leave.flush({ ...player, count: 2 });
    fixture.detectChanges();

    expect(text()).toContain('2 players in the queue');
    expect(text()).toContain('You have left the Late League queue');
    expect(button()!.textContent).toContain('Join the queue');
  });

  it('shows why a join was refused, and loads the status again', () => {
    create();
    show(player);

    button()!.click();
    http.expectOne('/api/queue/join').flush('You are already in a Late League match!', { status: 409, statusText: 'Conflict' });
    statusRequest().flush({ ...player, inMatch: true });
    fixture.detectChanges();

    expect(text()).toContain('You are already in a Late League match!');
    expect(text()).toContain('You are in a Late League match');
  });

  it('says something went wrong when a failure gives no reason', () => {
    create();
    show(player);

    fixture.componentInstance.leave();
    http.expectOne('/api/queue/leave').flush(null, { status: 500, statusText: 'Server Error' });
    statusRequest().flush(player);
    fixture.detectChanges();

    expect(text()).toContain('Something went wrong');
  });

  it('says so when the queue cannot be loaded', () => {
    create();
    statusRequest().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text()).toContain('The Late League queue could not be loaded');
  });

  it('keeps the queue shown when a later load fails', () => {
    create();
    show(anonymous);
    fixture.componentInstance.load();
    statusRequest().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text()).toContain('3 players in the queue');
  });

  it('shows the size the hub sends; a visitor needs nothing else', () => {
    create();
    show(anonymous);

    handlerFor('QueueCount')(5);
    fixture.detectChanges();
    expect(text()).toContain('5 players in the queue');
  });

  it('loads a queued player\'s status again when the size changes, not when it is the same', () => {
    create();
    show({ ...player, inQueue: true });

    handlerFor('QueueCount')(3);
    handlerFor('QueueCount')(1);
    fixture.detectChanges();
    expect(text()).toContain('1 player in the queue');
    statusRequest().flush({ ...player, count: 1, inMatch: true });
    fixture.detectChanges();
    expect(text()).toContain('You are in a Late League match');
  });

  it('only shows the new size to a player outside the queue: their status did not change', () => {
    create();
    show(player);

    handlerFor('QueueCount')(5);
    fixture.detectChanges();
    expect(text()).toContain('5 players in the queue');
    http.expectNone('/api/queue');
  });

  it('loads when the hub sends before the status is in, and shows the newer answer', () => {
    create();
    handlerFor('QueueCount')(7);

    const [first, second] = http.match('/api/queue');
    second.flush({ ...anonymous, count: 7 });
    first.flush(anonymous);
    fixture.detectChanges();
    expect(text()).toContain('7 players in the queue');
  });

  it('recovers from a failed first load when the hub sends', () => {
    create();
    statusRequest().flush(null, { status: 502, statusText: 'Bad Gateway' });
    fixture.detectChanges();
    expect(text()).toContain('The Late League queue could not be loaded');

    handlerFor('QueueCount')(2);
    show({ ...anonymous, count: 2 });
    expect(text()).toContain('2 players in the queue');
  });

  it('drops a status read before a join that answers after it', () => {
    create();
    show(player);

    fixture.componentInstance.load();
    const stale = statusRequest();
    button()!.click();
    http.expectOne('/api/queue/join').flush({ ...player, count: 4, inQueue: true });
    stale.flush(player);
    fixture.detectChanges();

    expect(button()!.textContent).toContain('Leave the queue');
  });

  it('loads once a join is done when the size changed meanwhile', () => {
    create();
    show(player);

    button()!.click();
    handlerFor('QueueCount')(5);
    http.expectNone('/api/queue');
    http.expectOne('/api/queue/join').flush({ ...player, count: 4, inQueue: true });
    statusRequest().flush({ ...player, count: 5, inQueue: true });
    fixture.detectChanges();

    expect(text()).toContain('5 players in the queue');
  });

  it('offers no Join where matchmaking is off, but still lets a queued player leave', () => {
    create();
    show({ ...player, open: false });
    expect(text()).toContain('Late League is off here');
    expect(button()).toBeNull();

    fixture.componentInstance.load();
    show({ ...player, open: false, inQueue: true });
    expect(button()!.textContent).toContain('Leave the queue');
  });

  it('loads again after a reconnect, and after a sign-in or out', () => {
    create();
    show({ ...player, inQueue: true });
    fixture.componentInstance.message.set('You have joined the Late League queue');

    connection.onreconnected.calls.mostRecent().args[0]();
    statusRequest().flush({ ...player, inQueue: true });

    TestBed.inject(AuthSession).signedInOrOut();
    statusRequest().flush(anonymous);
    fixture.detectChanges();

    expect(fixture.componentInstance.message()).toBeNull();
    expect(text()).toContain('Sign in');
  });

  it('loads again once connected: a size sent before then was missed', async () => {
    connection.start.and.returnValue(Promise.resolve());
    create();
    show(anonymous);
    await settled();

    show({ ...anonymous, count: 4 });
    expect(text()).toContain('4 players in the queue');
  });

  it('loads again when the hub says this player\'s status changed', () => {
    create();
    show({ ...player, inMatch: true });

    // Their match ended: the size is the same, but their status is not
    handlerFor('QueueStatusChanged')(undefined);
    show(player);
    expect(button()!.textContent).toContain('Join the queue');
  });

  it('lets a queued player without a gamertag leave', () => {
    create();
    show({ ...player, hasGamertag: false, inQueue: true });
    expect(button()!.textContent).toContain('Leave the queue');
    expect(text()).not.toContain('Set your gamertag');
  });
});
