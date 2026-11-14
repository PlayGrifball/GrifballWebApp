import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PLATFORM_ID, signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HubConnectionBuilder, HubConnectionState, type HubConnection, type IStreamSubscriber } from '@microsoft/signalr';
import { AccountService } from '@core/auth/account.service';
import { CHAT_STORAGE_KEY, MAX_QUESTION_LENGTH } from './chat.store';
import { ChatComponent } from './chat.component';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  let stream: jasmine.Spy;
  let answers: { subscriber: IStreamSubscriber<string>; disposed: boolean }[];
  const isLoggedIn = signal(false);

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');
  const textarea = () => el().querySelector<HTMLTextAreaElement>('textarea')!;
  const button = (label: string) => [...el().querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent!.trim() === label);
  const turns = () => [...el().querySelectorAll('.chat__turn')].map(t => [t.querySelector('.chat__who')!.textContent!.trim(), t.querySelector('.chat__text')!.textContent]);
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
    sessionStorage.removeItem(CHAT_STORAGE_KEY);
    isLoggedIn.set(false);
    answers = [];
    stream = jasmine.createSpy('stream').and.callFake(() => ({
      subscribe: (subscriber: IStreamSubscriber<string>) => {
        const entry = { subscriber, disposed: false };
        answers.push(entry);
        return { dispose: () => { entry.disposed = true; } };
      },
    }));
    const connection = {
      start: () => Promise.resolve(), stop: () => Promise.resolve(), on: () => undefined, onreconnected: () => undefined,
      onclose: () => undefined, stream, state: HubConnectionState.Connected,
    };
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as unknown as HubConnection);
    TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [provideRouter([]), { provide: PLATFORM_ID, useValue: 'browser' }, { provide: AccountService, useValue: { isLoggedIn } }],
    });
    fixture = TestBed.createComponent(ChatComponent);
    fixture.detectChanges();
  });

  afterEach(() => sessionStorage.removeItem(CHAT_STORAGE_KEY));

  it('says what it is, and asks a signed-out visitor to name the player or log in', () => {
    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Chat');
    expect(text()).toContain('name the player you mean');
    expect(el().querySelector('a[href="/login"]')).not.toBeNull();
    expect(text()).toContain('No questions yet');

    isLoggedIn.set(true);
    fixture.detectChanges();
    expect(text()).not.toContain('name the player you mean');
  });

  it('asks with the button, empties the box, and shows the answer as it streams in', () => {
    expect(button('Ask')!.disabled).toBeTrue();
    type('Who won?');
    button('Ask')!.click();
    fixture.detectChanges();

    expect(stream).toHaveBeenCalledOnceWith('Ask', { question: 'Who won?', history: [] });
    expect(textarea().value).toBe('');
    expect(button('Ask')!.disabled).toBeTrue();
    expect(el().querySelector('[role="log"]')!.getAttribute('aria-busy')).toBe('true');

    answer().subscriber.next('The Grifs.');
    fixture.detectChanges();
    expect(turns()).toEqual([['You', 'Who won?'], ['Grif', 'The Grifs.']]);

    answer().subscriber.complete();
    fixture.detectChanges();
    expect(button('Stop')).toBeUndefined();
    expect(el().querySelector('[role="log"]')!.getAttribute('aria-busy')).toBe('false');
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

  it('takes no question over the limit, and counts the characters', () => {
    expect(textarea().maxLength).toBe(MAX_QUESTION_LENGTH);
    type('a'.repeat(MAX_QUESTION_LENGTH + 1));
    expect(button('Ask')!.disabled).toBeTrue();
    press('Enter');
    expect(stream).not.toHaveBeenCalled();
    expect(text()).toContain(`${MAX_QUESTION_LENGTH + 1} / ${MAX_QUESTION_LENGTH}`);
  });

  it('shows why an answer failed, as an alert', () => {
    type('Who won?');
    press('Enter');

    answer().subscriber.error(new Error('HubException: That took too long to answer: ask again, or ask something simpler.'));
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')!.textContent!.trim()).toBe('That took too long to answer: ask again, or ask something simpler.');
  });

  it('stops an answer, and starts a new conversation', () => {
    expect(button('New conversation')!.disabled).toBeTrue();
    type('Who won?');
    press('Enter');
    answer().subscriber.next('The ');
    fixture.detectChanges();
    expect(button('New conversation')!.disabled).toBeTrue();

    button('Stop')!.click();
    fixture.detectChanges();
    expect(answer().disposed).toBeTrue();
    expect(turns()).toEqual([['You', 'Who won?'], ['Grif', 'The ']]);

    button('New conversation')!.click();
    fixture.detectChanges();
    expect(turns()).toEqual([]);
    expect(text()).toContain('No questions yet');
  });
});
