import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, PLATFORM_ID, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { HubConnectionBuilder, HubConnectionState, type HubConnection, type IStreamSubscriber } from '@microsoft/signalr';
import type { ChatActionDto, ChatConfirmDto } from '@api/models';
import { AccountService } from '@core/auth/account.service';
import { CHAT_STORAGE_KEY, CONFIRMED } from '../chat.store';
import { ChatComponent } from '../chat.component';
import { FlipyapChatComponent } from './flipyapChat.component';

@Component({ template: '' })
class Blank {}

describe('FlipyapChatComponent', () => {
  let fixture: ComponentFixture<FlipyapChatComponent>;
  let stream: jasmine.Spy;
  let answers: { subscriber: IStreamSubscriber<string>; disposed: boolean }[];
  let hubWaiting: ChatActionDto | null;
  let closed: jasmine.Spy;
  let talking: jasmine.Spy;
  const isLoggedIn = signal(false);

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');
  const textarea = () => el().querySelector<HTMLTextAreaElement>('textarea')!;
  const button = (label: string) => [...el().querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent!.trim() === label);
  const turns = () => [...el().querySelectorAll('.flipyap-chat__turn')].map(t => [t.querySelector('.flipyap-chat__who')!.textContent!.trim(), t.querySelector('.flipyap-chat__text')!.textContent]);
  const answer = () => answers.at(-1)!;

  function type(question: string): void {
    textarea().value = question;
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function press(key: string, shiftKey = false): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, shiftKey, cancelable: true });
    textarea().dispatchEvent(event);
    fixture.detectChanges();
    return event;
  }

  beforeEach(() => {
    // The page's title outlives a test: one set before must not reach the next ask
    document.title = '';
    sessionStorage.removeItem(CHAT_STORAGE_KEY);
    isLoggedIn.set(false);
    answers = [];
    hubWaiting = null;
    const invoke = jasmine.createSpy('invoke').and.callFake((method: string) =>
      Promise.resolve(method === 'Waiting' ? hubWaiting : { models: [{ id: 'ollama', name: 'llama3.2:3b' }], selected: 'ollama' }));
    stream = jasmine.createSpy('stream').and.callFake(() => ({
      subscribe: (subscriber: IStreamSubscriber<string>) => {
        const entry = { subscriber, disposed: false };
        answers.push(entry);
        return { dispose: () => { entry.disposed = true; } };
      },
    }));
    const connection = {
      start: () => Promise.resolve(), stop: () => Promise.resolve(), on: () => undefined,
      onreconnected: () => undefined, onclose: () => undefined, stream, invoke,
      state: HubConnectionState.Connected,
    };
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as unknown as HubConnection);
    TestBed.configureTestingModule({
      imports: [FlipyapChatComponent],
      providers: [provideRouter([{ path: '**', component: Blank }]), { provide: PLATFORM_ID, useValue: 'browser' }, { provide: AccountService, useValue: { isLoggedIn } }],
    });
    fixture = TestBed.createComponent(FlipyapChatComponent);
    closed = jasmine.createSpy('closed');
    talking = jasmine.createSpy('talking');
    fixture.componentInstance.closed.subscribe(closed);
    fixture.componentInstance.talking.subscribe(talking);
    document.body.append(el());
    fixture.detectChanges();
  });

  afterEach(() => {
    el().remove();
    sessionStorage.removeItem(CHAT_STORAGE_KEY);
  });

  it('puts the cursor in the question box, and asks a signed-out visitor to name the player', () => {
    expect(document.activeElement).toBe(textarea());
    expect(text()).toContain('Ask Flipyap about seasons');
    expect(text()).toContain("Flipyap no know who you are");

    isLoggedIn.set(true);
    fixture.detectChanges();
    expect(text()).not.toContain("Flipyap no know who you are");
  });

  it('tells the chat which page he stands on, without its query or fragment, and its title', async () => {
    await TestBed.inject(Router).navigateByUrl('/season/12?tab=stats#top');
    TestBed.inject(Title).setTitle('Spring 2026 - Grifball');
    type('Who is first?');
    button('Ask')!.click();
    fixture.detectChanges();

    expect(stream).toHaveBeenCalledOnceWith('Ask', { question: 'Who is first?', history: [], model: null, autoApprove: null, page: '/season/12', pageTitle: 'Spring 2026 - Grifball' });

    answer().subscriber.complete();
    await TestBed.inject(Router).navigateByUrl('/leaderboards');
    TestBed.inject(Title).setTitle('Leaderboards - Grifball');
    type('And here?');
    button('Ask')!.click();
    expect(stream.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ page: '/leaderboards', pageTitle: 'Leaderboards - Grifball' }));
  });

  it('asks, shows the answer as Flipyap as it streams in, and talks while it does', () => {
    type('Who won?');
    button('Ask')!.click();
    fixture.detectChanges();

    expect(stream).toHaveBeenCalledOnceWith('Ask', { question: 'Who won?', history: [], model: null, autoApprove: null, page: '/', pageTitle: null });
    expect(textarea().value).toBe('');
    expect(talking).toHaveBeenCalledWith(true);

    answer().subscriber.next('The Grifs.');
    fixture.detectChanges();
    expect(turns()).toEqual([['You', 'Who won?'], ['Flipyap', 'The Grifs.']]);

    answer().subscriber.complete();
    fixture.detectChanges();
    expect(talking.calls.mostRecent().args).toEqual([false]);
    expect(button('Stop')).toBeUndefined();
  });

  it('asks on Enter, and starts a new line on Shift+Enter', () => {
    type('Who won?');
    expect(press('Enter', true).defaultPrevented).toBeFalse();
    expect(stream).not.toHaveBeenCalled();

    expect(press('Enter').defaultPrevented).toBeTrue();
    expect(stream).toHaveBeenCalledTimes(1);
  });

  it('keeps the question when it could not be asked', () => {
    type('Who won?');
    press('Enter');
    type('And before?');
    press('Enter');

    expect(stream).toHaveBeenCalledTimes(1);
    expect(textarea().value).toBe('And before?');
  });

  it('stops an answer, and starts a new conversation', () => {
    type('Who won?');
    press('Enter');
    button('Stop')!.click();
    fixture.detectChanges();
    expect(answer().disposed).toBeTrue();

    button('New')!.click();
    fixture.detectChanges();
    expect(turns()).toEqual([]);
  });

  it('shows why an answer failed, as an alert', () => {
    type('Who won?');
    press('Enter');

    answer().subscriber.error(new Error('HubException: That took too long to answer: ask again, or ask something simpler.'));
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')!.textContent!.trim()).toBe('That took too long to answer: ask again, or ask something simpler.');
  });

  it('shows a change waiting for the user, and runs it only once they confirm it', async () => {
    hubWaiting = { id: 'a1', description: 'forfeit_season_match(seasonMatchID: 12, side: home)' };
    type('Forfeit match 12');
    press('Enter');
    answer().subscriber.complete();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();

    expect(el().querySelector('.flipyap-chat__confirm')!.textContent).toContain('forfeit_season_match(seasonMatchID: 12, side: home)');
    stream.calls.reset();

    button('Confirm')!.click();
    fixture.detectChanges();

    const [method, sent] = stream.calls.mostRecent().args as [string, ChatConfirmDto];
    expect(method).toBe('Confirm');
    expect(sent).toEqual(jasmine.objectContaining({ actionId: 'a1', approved: true, page: '/', pageTitle: null }));
    expect(turns().at(-2)).toEqual(['You', CONFIRMED]);
  });

  it('cancels a waiting change', async () => {
    hubWaiting = { id: 'a1', description: 'forfeit_season_match(seasonMatchID: 12, side: home)' };
    type('Forfeit match 12');
    press('Enter');
    answer().subscriber.complete();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    stream.calls.reset();

    button('Cancel')!.click();
    fixture.detectChanges();

    expect(stream.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ actionId: 'a1', approved: false }));
  });

  it('says it is closed with the close button and with the link to the full chat', () => {
    el().querySelector<HTMLButtonElement>('button[aria-label="Close chat"]')!.click();
    expect(closed).toHaveBeenCalledTimes(1);

    el().querySelector<HTMLAnchorElement>('a[href="/chat"]')!.click();
    expect(closed).toHaveBeenCalledTimes(2);
  });

  it('keeps the stream going when he is left for /chat: the component goes, the shared store does not', () => {
    type('Who won?');
    button('Ask')!.click();
    fixture.detectChanges();
    answer().subscriber.next('The ');
    fixture.detectChanges();

    fixture.destroy();

    expect(answer().disposed).withContext('leaving for /chat must not cancel the stream').toBeFalse();

    const onChatPage = TestBed.createComponent(ChatComponent);
    onChatPage.detectChanges();
    const chatPageText = (onChatPage.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
    expect(chatPageText).toContain('Who won?');
    expect(chatPageText).toContain('The ');

    answer().subscriber.next('Grifs.');
    answer().subscriber.complete();
    onChatPage.detectChanges();
    expect((onChatPage.nativeElement as HTMLElement).textContent).toContain('The Grifs.');
  });

  it('keeps the newest message in view', async () => {
    const log = el().querySelector<HTMLElement>('[role="log"]')!;
    log.style.height = '60px';
    log.style.overflowY = 'auto';
    type('Who won?');
    press('Enter');
    answer().subscriber.next('A long answer. '.repeat(40));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(log.scrollTop).toBeGreaterThan(0);
    expect(log.scrollTop + log.clientHeight).toBeGreaterThanOrEqual(log.scrollHeight - 1);
  });
});
