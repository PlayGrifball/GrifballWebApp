import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthSession } from '@core/auth/authSession.service';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';
import type { QueueStatusDto } from '@api/models';
import { LateLeagueQueueComponent } from './lateLeagueQueue.component';

/** The live service as the page sees it: who watches what, and a message to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void, stopped: boolean }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    const watch = { topics, listener, stopped: false };
    this.watching.push(watch);
    return () => watch.stopped = true;
  }

  /** Sends `message` to those watching its topic; a null topic (the connection came back) to everyone. */
  send(message: LiveMessage): void {
    for (const watch of this.watching.filter(w => !w.stopped && (message.topic === null || w.topics.includes(message.topic))))
      watch.listener(message);
  }

  get active(): (readonly string[])[] {
    return this.watching.filter(w => !w.stopped).map(w => w.topics);
  }
}

describe('LateLeagueQueueComponent', () => {
  let live: FakeLive;
  let fixture: ComponentFixture<LateLeagueQueueComponent>;
  let http: HttpTestingController;

  const anonymous: QueueStatusDto = { count: 3, open: true, signedIn: false, hasGamertag: false, inQueue: false, inMatch: false };
  const player: QueueStatusDto = { count: 3, open: true, signedIn: true, hasGamertag: true, inQueue: false, inMatch: false };

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');
  const button = () => el().querySelector<HTMLButtonElement>('button');
  const statusRequest = () => http.expectOne('/api/queue');
  /** The viewer's own place in the queue changed (a notice: no data comes with it), and the page's wait for more changes ran out. */
  const liveChange = () => {
    live.send({ topic: 'queue:me' });
    jasmine.clock().tick(LIVE_RELOAD_MS);
  };

  function create(platform = 'browser'): void {
    TestBed.configureTestingModule({
      imports: [LateLeagueQueueComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: PLATFORM_ID, useValue: platform },
        { provide: LiveService, useValue: live }],
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
    jasmine.clock().install();
    // No spread: a load runs LIVE_RELOAD_MS after a change
    spyOn(Math, 'random').and.returnValue(0);
    live = new FakeLive();
  });

  afterEach(() => {
    http.verify();
    jasmine.clock().uninstall();
  });

  it('calls itself Late League, and links to the Late League page unless told not to', () => {
    create();
    show(anonymous);
    expect(el().querySelector('h2')!.textContent!.trim()).toBe('Late League queue');
    expect(el().querySelector('a[href="/late-league"]')!.textContent!.trim()).toBe('Late League games and stats');
    fixture.componentRef.setInput('showLink', false);
    fixture.detectChanges();
    expect(el().querySelector('a[href="/late-league"]')).toBeNull();
  });

  it("watches the queue and the viewer's own place in it, and loads the status", () => {
    create();
    expect(live.active).toEqual([['queue'], ['queue:me']]);
    expect(text()).toContain('Loading the Late League queue');
    show(anonymous);
    expect(text()).toContain('3 players in the queue');
  });

  it('stops watching when the page goes, and drops a load still waiting', () => {
    create();
    show(anonymous);
    live.send({ topic: 'queue:me' });
    fixture.destroy();
    jasmine.clock().tick(LIVE_RELOAD_MS);
    expect(live.active).toEqual([]);
    http.expectNone('/api/queue');
  });

  it('does nothing on the server: no request, nothing watched', () => {
    create('server');
    expect(live.watching).toEqual([]);
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
    liveChange();
    statusRequest().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text()).toContain('3 players in the queue');
  });

  it('shows the size pushed with a change to the queue at once, with no request', () => {
    create();
    show(anonymous);

    live.send({ topic: 'queue', data: { count: 5 } });
    fixture.detectChanges();
    expect(text()).toContain('5 players in the queue');
    jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
    http.expectNone('/api/queue');
  });

  it('loads the status when a size is pushed before it has one', () => {
    create();
    statusRequest().flush(null, { status: 502, statusText: 'Bad Gateway' });

    live.send({ topic: 'queue', data: { count: 5 } });
    jasmine.clock().tick(LIVE_RELOAD_MS);
    show({ ...anonymous, count: 5 });
    expect(text()).toContain('5 players in the queue');
  });

  it("loads the status again once a burst of changes to the viewer's own place settles", () => {
    create();
    show(player);

    live.send({ topic: 'queue:me' });
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    live.send({ topic: 'queue:me' });
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    http.expectNone('/api/queue');

    jasmine.clock().tick(1);
    show({ ...player, inQueue: true });
    expect(button()!.textContent).toContain('Leave the queue');
  });

  it('loads when a live change comes before the status is in, and shows the newer answer', () => {
    create();
    liveChange();

    const [first, second] = http.match('/api/queue');
    second.flush({ ...anonymous, count: 7 });
    first.flush(anonymous);
    fixture.detectChanges();
    expect(text()).toContain('7 players in the queue');
  });

  it('recovers from a failed first load on a live change', () => {
    create();
    statusRequest().flush(null, { status: 502, statusText: 'Bad Gateway' });
    fixture.detectChanges();
    expect(text()).toContain('The Late League queue could not be loaded');

    liveChange();
    show({ ...anonymous, count: 2 });
    expect(text()).toContain('2 players in the queue');
  });

  it('drops a status read before a join that answers after it', () => {
    create();
    show(player);

    liveChange();
    const stale = statusRequest();
    button()!.click();
    http.expectOne('/api/queue/join').flush({ ...player, count: 4, inQueue: true });
    stale.flush(player);
    fixture.detectChanges();

    expect(button()!.textContent).toContain('Leave the queue');
  });

  it('loads once a join is done when the queue changed meanwhile', () => {
    create();
    show(player);

    button()!.click();
    liveChange();
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

    liveChange();
    show({ ...player, open: false, inQueue: true });
    expect(button()!.textContent).toContain('Leave the queue');
  });

  it('loads again after a live change, and after a sign-in or out', () => {
    create();
    show({ ...player, inQueue: true });
    button()!.click();
    http.expectOne('/api/queue/leave').flush(player);
    expect(fixture.componentInstance.message()).toBe('You have left the Late League queue');

    liveChange();
    statusRequest().flush({ ...player, inQueue: true });

    TestBed.inject(AuthSession).signedInOrOut();
    statusRequest().flush(anonymous);
    fixture.detectChanges();

    expect(fixture.componentInstance.message()).toBeNull();
    expect(text()).toContain('Sign in');
  });

  it("loads again when the viewer's own place changes", () => {
    create();
    show({ ...player, inMatch: true });

    // Their match ended: the size is the same, but their status is not
    liveChange();
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
