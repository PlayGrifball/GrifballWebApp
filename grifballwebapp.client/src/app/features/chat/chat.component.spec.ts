import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PLATFORM_ID, signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HubConnectionBuilder, HubConnectionState, type HubConnection, type IStreamSubscriber } from '@microsoft/signalr';
import type { ChatActionDto, ChatConfirmDto } from '@api/models';
import { AccountService } from '@core/auth/account.service';
import { CHAT_STORAGE_KEY, CONFIRMED, MAX_QUESTION_LENGTH } from './chat.store';
import { ChatComponent } from './chat.component';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  let stream: jasmine.Spy;
  let answers: { subscriber: IStreamSubscriber<string>; disposed: boolean }[];
  let invoke: jasmine.Spy;
  /** What the hub says: the user's models, and the change waiting for their answer. */
  let hubModels: object;
  let hubWaiting: ChatActionDto | null;
  /** The connection coming back up: the store loads the user's models again. */
  let reconnected: () => void;
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
    hubModels = { models: [{ id: 'ollama', name: 'llama3.2:3b' }], selected: 'ollama' };
    hubWaiting = null;
    invoke = jasmine.createSpy('invoke').and.callFake((method: string) => Promise.resolve(method === 'Waiting' ? hubWaiting : hubModels));
    stream = jasmine.createSpy('stream').and.callFake(() => ({
      subscribe: (subscriber: IStreamSubscriber<string>) => {
        const entry = { subscriber, disposed: false };
        answers.push(entry);
        return { dispose: () => { entry.disposed = true; } };
      },
    }));
    const connection = {
      start: () => Promise.resolve(), stop: () => Promise.resolve(), on: () => undefined,
      onreconnected: (callback: () => void) => { reconnected = callback; }, onclose: () => undefined, stream, invoke,
      state: HubConnectionState.Connected,
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
    expect(text()).toContain('Name the player you mean');
    expect(el().querySelector('a[href="/login"]')).not.toBeNull();
    expect(text()).toContain('No questions yet');

    isLoggedIn.set(true);
    fixture.detectChanges();
    expect(text()).not.toContain('Name the player you mean');
  });

  it('asks with the button, empties the box, and shows the answer as it streams in', () => {
    expect(button('Ask')!.disabled).toBeTrue();
    type('Who won?');
    button('Ask')!.click();
    fixture.detectChanges();

    expect(stream).toHaveBeenCalledOnceWith('Ask', { question: 'Who won?', history: [], model: null, autoApprove: null, page: null, pageTitle: null });
    expect(textarea().value).toBe('');
    expect(button('Ask')!.disabled).toBeTrue();
    expect(el().querySelector('[role="log"]')!.getAttribute('aria-busy')).toBe('true');

    answer().subscriber.next('The Grifs.');
    fixture.detectChanges();
    expect(turns()).toEqual([['You', 'Who won?'], ['Flipyap', 'The Grifs.']]);

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
    expect(turns()).toEqual([['You', 'Who won?'], ['Flipyap', 'The ']]);

    button('New conversation')!.click();
    fixture.detectChanges();
    expect(turns()).toEqual([]);
    expect(text()).toContain('No questions yet');
  });
  /** The connection comes up again as a user with `models`: the page shows them. */
  async function withModels(models: object): Promise<void> {
    hubModels = models;
    reconnected();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    // The select shows its selected option's name once its options are in
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows no model picker with only one model to use', async () => {
    expect(el().querySelector('mat-select')).withContext('none before the models are known').toBeNull();

    await withModels({ models: [{ id: 'ollama', name: 'llama3.2:3b' }], selected: 'ollama' });

    expect(el().querySelector('mat-select')).toBeNull();
    expect(text()).not.toContain('this month');
  });

  it("shows this month's spend with only one model to use, a paid one, though no picker", async () => {
    await withModels({ models: [{ id: 'claude-haiku', name: 'Claude Haiku 5.5' }], selected: 'claude-haiku', spent: 2.25, limit: 10 });

    expect(el().querySelector('mat-select')).toBeNull();
    expect(text()).toContain('$2.25 of $10.00 this month');
  });

  it("shows the models the user may use, the selected one, and this month's spend on paid ones", async () => {
    await withModels({
      models: [{ id: 'claude-haiku', name: 'Claude Haiku 5.5' }, { id: 'ollama', name: 'llama3.2:3b' }],
      selected: 'claude-haiku', spent: 1.5, limit: 10,
    });

    expect(el().querySelector('mat-select')?.textContent).toContain('Claude Haiku 5.5');
    expect(text()).toContain('$1.50 of $10.00 this month');
  });

  it('keeps the model the user chooses on the server', async () => {
    await withModels({
      models: [{ id: 'claude-haiku', name: 'Claude Haiku 5.5' }, { id: 'ollama', name: 'llama3.2:3b' }],
      selected: 'claude-haiku', spent: 0, limit: 10,
    });

    fixture.componentInstance.choose('ollama');

    expect(invoke).toHaveBeenCalledWith('Choose', 'ollama');
  });
  it('shows a change waiting for the user, and runs it only once they confirm it', async () => {
    hubWaiting = { id: 'a1', description: 'forfeit_season_match(seasonMatchID: 12, side: home)' };
    type('Forfeit match 12');
    press('Enter');
    answer().subscriber.complete();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();

    expect(el().querySelector('.chat__confirm')!.textContent).toContain('forfeit_season_match(seasonMatchID: 12, side: home)');
    expect(button('Cancel')).toBeDefined();
    stream.calls.reset();

    button('Confirm')!.click();
    fixture.detectChanges();

    const [method, sent] = stream.calls.mostRecent().args as [string, ChatConfirmDto];
    expect(method).toBe('Confirm');
    expect(sent).toEqual(jasmine.objectContaining({ actionId: 'a1', approved: true }));
    expect(turns().at(-2)).toEqual(['You', CONFIRMED]);
    expect(el().querySelector('.chat__confirm')).withContext('answered').toBeNull();
  });

  it('shows no change waiting when none does', async () => {
    type('Who won?');
    press('Enter');
    answer().subscriber.complete();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();

    expect(el().querySelector('.chat__confirm')).toBeNull();
    expect(button('Confirm')).toBeUndefined();
  });

  it("offers staff to run changes without asking, this conversation or as their default, saying what it risks", async () => {
    await withModels({ models: [{ id: 'ollama', name: 'llama3.2:3b' }], selected: 'ollama', autoApprove: false });
    const toggle = () => el().querySelector<HTMLButtonElement>('.chat__approve button[role="switch"]')!;
    expect(toggle().getAttribute('aria-checked')).toBe('false');
    expect(text()).toContain('talked it into');
    expect(button('Make this my default')).toBeUndefined();

    toggle().click();
    fixture.detectChanges();
    expect(toggle().getAttribute('aria-checked')).toBe('true');
    button('Make this my default')!.click();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();

    expect(invoke).toHaveBeenCalledWith('SetAutoApprove', true);
  });

  it('offers no auto-approve to someone with no change to approve', async () => {
    await withModels({ models: [{ id: 'ollama', name: 'llama3.2:3b' }], selected: 'ollama' });

    expect(el().querySelector('.chat__approve')).toBeNull();
  });
});
