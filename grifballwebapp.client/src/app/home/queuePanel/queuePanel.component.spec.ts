import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { HttpTransportType, HubConnectionBuilder } from '@microsoft/signalr';
import { AuthSession } from '../../authSession.service';
import type { QueueStatusDto } from '../../api/dtos/queueStatusDto';
import { QUEUE_HUB_URL, QueuePanelComponent } from './queuePanel.component';

describe('QueuePanelComponent', () => {
  let connection: { start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy; onreconnected: jasmine.Spy };
  let withUrl: jasmine.Spy;
  let fixture: ComponentFixture<QueuePanelComponent>;
  let http: HttpTestingController;

  const anonymous: QueueStatusDto = { count: 3, signedIn: false, hasGamertag: false, inQueue: false, inMatch: false };
  const player: QueueStatusDto = { count: 3, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');
  const button = () => el().querySelector<HTMLButtonElement>('button');
  const statusRequest = () => http.expectOne('/api/Queue/Status');
  const handlerFor = (event: string) => connection.on.calls.all().find(c => c.args[0] === event)!.args[1] as (value: unknown) => void;

  function create(platform = 'browser'): void {
    TestBed.configureTestingModule({
      imports: [QueuePanelComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: PLATFORM_ID, useValue: platform }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(QueuePanelComponent);
    fixture.detectChanges();
  }

  function show(status: QueueStatusDto): void {
    statusRequest().flush(status);
    fixture.detectChanges();
  }

  beforeEach(() => {
    connection = {
      start: jasmine.createSpy('start').and.returnValue(Promise.resolve()),
      stop: jasmine.createSpy('stop').and.returnValue(Promise.resolve()),
      on: jasmine.createSpy('on'),
      onreconnected: jasmine.createSpy('onreconnected'),
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as any);
  });

  afterEach(() => http.verify());

  it('connects to the queue hub over a WebSocket and loads the status', () => {
    create();
    expect(withUrl.calls.mostRecent().args[0]).toBe(QUEUE_HUB_URL);
    expect(QUEUE_HUB_URL).toBe('hub/QueueHub');
    expect(withUrl.calls.mostRecent().args[1]).toEqual({ skipNegotiation: true, transport: HttpTransportType.WebSockets });
    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(text()).toContain('Loading the queue');
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
    expect(text()).toContain('Loading the queue');
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
    expect(text()).toContain('You are in a match');
    expect(button()).toBeNull();
  });

  it('joins the queue, then offers to leave it', () => {
    create();
    show(player);
    expect(button()!.textContent).toContain('Join the queue');

    button()!.click();
    fixture.detectChanges();
    expect(button()!.disabled).toBeTrue();
    const join = http.expectOne('/api/Queue/Join');
    expect(join.request.method).toBe('POST');
    join.flush({ ...player, count: 4, inQueue: true });
    fixture.detectChanges();

    expect(text()).toContain('4 players in the queue');
    expect(text()).toContain('You have joined the queue');
    expect(button()!.textContent).toContain('Leave the queue');
    expect(button()!.disabled).toBeFalse();
  });

  it('leaves the queue', () => {
    create();
    show({ ...player, inQueue: true });

    button()!.click();
    const leave = http.expectOne('/api/Queue/Leave');
    expect(leave.request.method).toBe('POST');
    leave.flush({ ...player, count: 2 });
    fixture.detectChanges();

    expect(text()).toContain('2 players in the queue');
    expect(text()).toContain('You have left the queue');
    expect(button()!.textContent).toContain('Join the queue');
  });

  it('shows why a join was refused, and loads the status again', () => {
    create();
    show(player);

    button()!.click();
    http.expectOne('/api/Queue/Join').flush('You are already in a match!', { status: 409, statusText: 'Conflict' });
    statusRequest().flush({ ...player, inMatch: true });
    fixture.detectChanges();

    expect(text()).toContain('You are already in a match!');
    expect(text()).toContain('You are in a match');
  });

  it('says something went wrong when a failure gives no reason', () => {
    create();
    show(player);

    fixture.componentInstance.leave();
    http.expectOne('/api/Queue/Leave').flush(null, { status: 500, statusText: 'Server Error' });
    statusRequest().flush(player);
    fixture.detectChanges();

    expect(text()).toContain('Something went wrong');
  });

  it('says so when the queue cannot be loaded', () => {
    create();
    statusRequest().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text()).toContain('The queue could not be loaded');
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

  it('loads a signed-in player\'s status again when the size changes, not when it is the same', () => {
    create();
    show(player);

    handlerFor('QueueCount')(3);
    handlerFor('QueueCount')(1);
    fixture.detectChanges();
    expect(text()).toContain('1 player in the queue');
    statusRequest().flush({ ...player, count: 1, inMatch: true });
    fixture.detectChanges();
    expect(text()).toContain('You are in a match');
  });

  it('ignores the hub until the status is in', () => {
    create();
    handlerFor('QueueCount')(7);
    show(anonymous);
    expect(text()).toContain('3 players in the queue');
  });

  it('loads again after a reconnect, and after a sign-in or out', () => {
    create();
    show({ ...player, inQueue: true });
    fixture.componentInstance.message.set('You have joined the queue');

    connection.onreconnected.calls.mostRecent().args[0]();
    statusRequest().flush({ ...player, inQueue: true });

    TestBed.inject(AuthSession).signedInOrOut();
    statusRequest().flush(anonymous);
    fixture.detectChanges();

    expect(fixture.componentInstance.message()).toBeNull();
    expect(text()).toContain('Sign in');
  });

  it('logs a connection that fails to start', async () => {
    connection.start.and.returnValue(Promise.reject(new Error('refused')));
    const log = spyOn(console, 'log');
    create();
    show(anonymous);
    await new Promise(r => setTimeout(r));
    expect(log).toHaveBeenCalledWith(jasmine.stringContaining('refused'));
  });
});
