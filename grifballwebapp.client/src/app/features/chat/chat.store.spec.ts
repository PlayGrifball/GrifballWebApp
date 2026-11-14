import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { HubConnectionBuilder, HubConnectionState, type HubConnection, type IStreamSubscriber } from '@microsoft/signalr';
import type { ChatRequestDto, ChatTurnDto } from '@api/models';
import { AuthSession } from '@core/auth/authSession.service';
import {
  CHAT_HUB_URL, CHAT_STORAGE_KEY, ChatStore, FAILED, HISTORY_LIMIT, MAX_HISTORY_MESSAGE_LENGTH, NOT_CONNECTED, chatErrorText,
} from './chat.store';

describe('ChatStore', () => {
  /** The hub connection: Connected unless a spec says otherwise, each stream it is asked for recorded. */
  let connection: {
    start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy; onreconnected: jasmine.Spy; onclose: jasmine.Spy; stream: jasmine.Spy;
    state: HubConnectionState;
  };
  let withUrl: jasmine.Spy;
  /** Each answer asked for: who reads it, and whether it was stopped. */
  let answers: { subscriber: IStreamSubscriber<string>; disposed: boolean }[];

  const turn = (fromBot: boolean, text: string): ChatTurnDto => ({ fromBot, text });

  function create(platform = 'browser'): ChatStore {
    TestBed.configureTestingModule({ providers: [ChatStore, { provide: PLATFORM_ID, useValue: platform }] });
    return TestBed.inject(ChatStore);
  }

  /** The request the last answer was asked with. */
  const asked = () => connection.stream.calls.mostRecent().args as [string, ChatRequestDto];
  const answer = () => answers.at(-1)!;

  beforeEach(() => {
    sessionStorage.removeItem(CHAT_STORAGE_KEY);
    answers = [];
    connection = {
      start: jasmine.createSpy('start').and.returnValue(Promise.resolve()),
      stop: jasmine.createSpy('stop').and.returnValue(Promise.resolve()),
      on: jasmine.createSpy('on'),
      onreconnected: jasmine.createSpy('onreconnected'),
      onclose: jasmine.createSpy('onclose'),
      stream: jasmine.createSpy('stream').and.callFake(() => ({
        subscribe: (subscriber: IStreamSubscriber<string>) => {
          const entry = { subscriber, disposed: false };
          answers.push(entry);
          return { dispose: () => { entry.disposed = true; } };
        },
      })),
      state: HubConnectionState.Connected,
    };
    withUrl = spyOn(HubConnectionBuilder.prototype, 'withUrl').and.callThrough();
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection as unknown as HubConnection);
  });

  afterEach(() => sessionStorage.removeItem(CHAT_STORAGE_KEY));

  it('connects to the chat hub', () => {
    create();
    expect(withUrl.calls.mostRecent().args[0]).toBe(CHAT_HUB_URL);
    expect(CHAT_HUB_URL).toBe('hub/ChatHub');
  });

  it('asks the question with the conversation so far, and streams the answer into it', () => {
    const store = create();

    expect(store.ask('  Who won?  ')).toBeTrue();
    expect(asked()).toEqual(['Ask', { question: 'Who won?', history: [] }]);
    expect(store.asking()).toBeTrue();
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, '')]);

    answer().subscriber.next('The ');
    answer().subscriber.next('Grifs.');
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, 'The Grifs.')]);
    answer().subscriber.complete();
    expect(store.asking()).toBeFalse();

    store.ask('And before?');
    expect(asked()[1]).toEqual({ question: 'And before?', history: [turn(false, 'Who won?'), turn(true, 'The Grifs.')] });
  });

  it('sends only as much history as the server reads', () => {
    const long = 'a'.repeat(MAX_HISTORY_MESSAGE_LENGTH + 10);
    const kept = Array.from({ length: HISTORY_LIMIT + 5 }, (_, i) => turn(i % 2 === 1, i === HISTORY_LIMIT + 4 ? long : `m${i}`));
    sessionStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(kept));
    const store = create();

    store.ask('Next?');

    const history = asked()[1].history;
    expect(history.length).toBe(HISTORY_LIMIT);
    expect(history[0].text).toBe('m5');
    expect(history.at(-1)!.text.length).toBe(MAX_HISTORY_MESSAGE_LENGTH);
  });

  it("keeps the conversation for the tab's session, and takes it up again", () => {
    const store = create();
    store.ask('Who won?');
    answer().subscriber.next('The Grifs.');
    answer().subscriber.complete();

    expect(JSON.parse(sessionStorage.getItem(CHAT_STORAGE_KEY)!)).toEqual([turn(false, 'Who won?'), turn(true, 'The Grifs.')]);
    TestBed.resetTestingModule();
    expect(create().turns()).toEqual([turn(false, 'Who won?'), turn(true, 'The Grifs.')]);
  });

  it('ignores what the tab kept when it is no conversation', () => {
    for (const kept of ['not json', '{"fromBot":true}', '[{"fromBot":"yes","text":"x"}]', '[null]']) {
      sessionStorage.setItem(CHAT_STORAGE_KEY, kept);
      TestBed.resetTestingModule();
      expect(create().turns()).withContext(kept).toEqual([]);
    }
  });

  it("shows the server's reason for a refusal, and leaves no empty answer", () => {
    const store = create();
    store.ask('Who won?');

    answer().subscriber.error(new Error('An error occurred on the server while streaming results. HubException: Wait for the answer to your last question first.'));

    expect(store.error()).toBe('Wait for the answer to your last question first.');
    expect(store.asking()).toBeFalse();
    expect(store.turns()).toEqual([turn(false, 'Who won?')]);
  });

  it('keeps what came of an answer that failed part way', () => {
    const store = create();
    store.ask('Who won?');
    answer().subscriber.next('The ');

    answer().subscriber.error(new Error('Connection lost'));

    expect(store.error()).toBe(FAILED);
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, 'The ')]);
  });

  it('asks nothing while an answer is on its way, for no question, or before it has connected', () => {
    const store = create();
    expect(store.ask('   ')).toBeFalse();

    store.ask('Who won?');
    expect(store.ask('And now?')).toBeFalse();
    expect(connection.stream).toHaveBeenCalledTimes(1);
    answer().subscriber.complete();

    connection.state = HubConnectionState.Reconnecting;
    expect(store.ask('And now?')).toBeFalse();
    expect(store.error()).toBe(NOT_CONNECTED);
    expect(connection.stream).toHaveBeenCalledTimes(1);
  });

  it('stops the answer on its way, keeping what came of it', () => {
    const store = create();
    store.ask('Who won?');
    answer().subscriber.next('The ');

    store.stop();

    expect(answer().disposed).toBeTrue();
    expect(store.asking()).toBeFalse();
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, 'The ')]);
    store.stop();
    expect(store.turns().length).toBe(2);
  });

  it('starts a new conversation on request and on a sign-in or out, stopping the answer on its way', () => {
    const store = create();
    store.ask('Who won?');
    answer().subscriber.complete();
    store.clear();
    expect(store.turns()).toEqual([]);
    expect(JSON.parse(sessionStorage.getItem(CHAT_STORAGE_KEY)!)).toEqual([]);

    store.ask('Who am I?');
    TestBed.inject(AuthSession).signedInOrOut();

    expect(answer().disposed).toBeTrue();
    expect(store.turns()).toEqual([]);
    expect(store.asking()).toBeFalse();
  });

  it('stops the answer on its way when the page goes', () => {
    const store = create();
    store.ask('Who won?');

    TestBed.resetTestingModule();

    expect(answer().disposed).toBeTrue();
    expect(store.asking()).toBeTrue();
  });

  it('on the server, neither connects nor asks', () => {
    sessionStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify([turn(false, 'Who won?')]));
    const store = create('server');

    expect(store.turns()).toEqual([]);
    expect(store.ask('Who won?')).toBeFalse();
    store.clear();
    expect(connection.stream).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(CHAT_STORAGE_KEY)).toBe(JSON.stringify([turn(false, 'Who won?')]));
  });

  it("chatErrorText: the server's reason, else that it failed", () => {
    expect(chatErrorText(new Error('An unexpected error occurred invoking \'Ask\' on the server. HubException: Ask a question.'))).toBe('Ask a question.');
    expect(chatErrorText(new Error('HubException: '))).toBe(FAILED);
    expect(chatErrorText(new Error('WebSocket closed'))).toBe(FAILED);
    expect(chatErrorText('nope')).toBe(FAILED);
  });
});
