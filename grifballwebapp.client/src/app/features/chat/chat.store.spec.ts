import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { HubConnectionBuilder, HubConnectionState, type HubConnection, type IStreamSubscriber } from '@microsoft/signalr';
import type { ChatActionDto, ChatConfirmDto, ChatModelsDto, ChatRequestDto, ChatTurnDto } from '@api/models';
import { AuthSession } from '@core/auth/authSession.service';
import {
  CANCELLED, CHAT_HUB_URL, CHAT_STORAGE_KEY, CONFIRMED, ChatStore, FAILED, HISTORY_LIMIT, MAX_HISTORY_MESSAGE_LENGTH, NOT_CONNECTED, STOP_SPEND_RELOAD_MS, chatErrorText,
} from './chat.store';

describe('ChatStore', () => {
  /** The hub connection: Connected unless a spec says otherwise, each stream it is asked for recorded. */
  let connection: {
    start: jasmine.Spy; stop: jasmine.Spy; on: jasmine.Spy; onreconnected: jasmine.Spy; onclose: jasmine.Spy; stream: jasmine.Spy; invoke: jasmine.Spy;
    state: HubConnectionState;
  };
  let withUrl: jasmine.Spy;
  /** Each answer asked for: who reads it, and whether it was stopped. */
  let answers: { subscriber: IStreamSubscriber<string>; disposed: boolean }[];
  /** The change the server says waits for the user's answer: none unless a spec says. */
  let waiting: ChatActionDto | null;

  const turn = (fromBot: boolean, text: string): ChatTurnDto => ({ fromBot, text });
  /** A sysadmin's models: Claude first (their default), then the league's own, and their spend this month. */
  const models: ChatModelsDto = {
    models: [{ id: 'claude-haiku', name: 'Claude Haiku 5.5' }, { id: 'ollama', name: 'llama3.2:3b' }],
    selected: 'claude-haiku',
    spent: 1.5,
    limit: 10,
  };

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
    waiting = null;
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
      invoke: jasmine.createSpy('invoke').and.callFake((method: string, model?: string) => {
        if (method === 'Waiting')
          return Promise.resolve(waiting);
        return Promise.resolve(method === 'Choose' ? { ...models, selected: model } : models);
      }),
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
    expect(asked()).toEqual(['Ask', { question: 'Who won?', history: [], model: null }]);
    expect(store.asking()).toBeTrue();
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, '')]);

    answer().subscriber.next('The ');
    answer().subscriber.next('Grifs.');
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, 'The Grifs.')]);
    answer().subscriber.complete();
    expect(store.asking()).toBeFalse();

    store.ask('And before?');
    expect(asked()[1]).toEqual({ question: 'And before?', history: [turn(false, 'Who won?'), turn(true, 'The Grifs.')], model: null });
  });

  it('sends only as much history as the server reads', () => {
    const long = 'a'.repeat(MAX_HISTORY_MESSAGE_LENGTH + 10);
    const kept = Array.from({ length: HISTORY_LIMIT + 5 }, (_, i) => turn(i % 2 === 1, i === HISTORY_LIMIT + 4 ? long : `m${i}`));
    sessionStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(kept));
    const store = create();

    store.ask('Next?');

    const history = asked()[1].history;
    expect(history).toHaveSize(HISTORY_LIMIT);
    expect(history[0].text).toBe('m5');
    expect(history.at(-1)!.text).toHaveSize(MAX_HISTORY_MESSAGE_LENGTH);
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
    const error = spyOn(console, 'error');
    const store = create();
    store.ask('Who won?');

    answer().subscriber.error(new Error('An error occurred on the server while streaming results. HubException: Wait for the answer to your last question first.'));

    expect(store.error()).toBe('Wait for the answer to your last question first.');
    expect(store.asking()).toBeFalse();
    expect(store.turns()).toEqual([turn(false, 'Who won?')]);
    // The server logged its own refusal
    expect(error).not.toHaveBeenCalled();
  });

  it('keeps what came of an answer that failed part way, and logs the failure', () => {
    const error = spyOn(console, 'error');
    const store = create();
    store.ask('Who won?');
    answer().subscriber.next('The ');

    answer().subscriber.error(new Error('Connection lost'));

    expect(store.error()).toBe(FAILED);
    expect(store.turns()).toEqual([turn(false, 'Who won?'), turn(true, 'The ')]);
    expect(error).toHaveBeenCalledOnceWith('Chat: the answer failed', jasmine.objectContaining({ message: 'Connection lost' }));
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
    expect(store.turns()).toHaveSize(2);
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

  describe('models', () => {
    /** The store with its connection come up, as the user the server says: their models loaded. */
    async function connected(): Promise<ChatStore> {
      const store = create();
      // The connection comes up (again): the store asks for the user's models
      (connection.onreconnected.calls.mostRecent().args[0] as () => void)();
      await new Promise(resolve => setTimeout(resolve));
      return store;
    }

    it('loads the models the user may use once the connection is up, and asks the selected one', async () => {
      const store = await connected();

      expect(connection.invoke).toHaveBeenCalledWith('Models');
      expect(store.models()).toEqual(models);
      store.ask('Who won?');
      expect(asked()[1]).toEqual({ question: 'Who won?', history: [], model: 'claude-haiku' });
    });

    it('asks with no model before the models are known: the server chooses', () => {
      const store = create();

      store.ask('Who won?');
      expect(asked()[1]).toEqual({ question: 'Who won?', history: [], model: null });
    });

    it('keeps the choice on the server, and asks the chosen model after', async () => {
      const store = await connected();

      await store.choose('ollama');

      expect(connection.invoke).toHaveBeenCalledWith('Choose', 'ollama');
      expect(store.models()?.selected).toBe('ollama');
      store.ask('Who won?');
      expect(asked()[1].model).toBe('ollama');
    });

    it("doesn't ask the server for the model already selected", async () => {
      const store = await connected();
      connection.invoke.calls.reset();

      await store.choose('claude-haiku');

      expect(connection.invoke).not.toHaveBeenCalled();
    });

    it("shows why the server refused a choice, and the user's models again, so the picker shows the one their questions go to", async () => {
      const store = await connected();
      const before = store.models();
      connection.invoke.and.callFake((method: string) => method === 'Choose'
        ? Promise.reject(new Error("HubException: That model isn't one you can use here: choose another."))
        : Promise.resolve({ ...models }));

      await store.choose('claude-opus');
      await new Promise(resolve => setTimeout(resolve));

      expect(store.error()).toBe("That model isn't one you can use here: choose another.");
      expect(store.models()?.selected).toBe('claude-haiku');
      expect(store.models()).withContext('a new value: the select is set back').not.toBe(before);
    });

    describe('stopped', () => {
      afterEach(() => jasmine.clock().uninstall());

      it("loads the spend again shortly after the user stops a paid model's answer, once the server has saved its cost", async () => {
        const store = await connected();
        connection.invoke.calls.reset();
        jasmine.clock().install();

        store.ask('Who won?');
        store.stop();

        expect(connection.invoke).withContext('the server is still saving it').not.toHaveBeenCalled();
        jasmine.clock().tick(STOP_SPEND_RELOAD_MS);
        expect(connection.invoke).toHaveBeenCalledWith('Models');
      });

      it('loads nothing after a stop once the page has gone', async () => {
        const store = await connected();
        connection.invoke.calls.reset();
        jasmine.clock().install();

        store.ask('Who won?');
        store.stop();
        TestBed.resetTestingModule();
        jasmine.clock().tick(STOP_SPEND_RELOAD_MS);

        expect(connection.invoke).not.toHaveBeenCalled();
      });
    });

    it("loads the spend again when a paid model's answer fails", async () => {
      const store = await connected();
      connection.invoke.calls.reset();

      store.ask('Who won?');
      answer().subscriber.error(new Error('HubException: That took too long to answer: ask again, or ask something simpler.'));

      expect(connection.invoke).toHaveBeenCalledWith('Models');
    });

    it('keeps no models when they fail to load', async () => {
      connection.invoke.and.returnValue(Promise.reject(new Error('down')));

      const store = await connected();

      expect(store.models()).toBeNull();
    });

    it('chooses nothing where there is no connection (the server\'s render)', async () => {
      const store = create('server');

      await store.choose('ollama');

      expect(connection.invoke).not.toHaveBeenCalled();
    });

    it("loads the spend again once a paid model's answer is done", async () => {
      const store = await connected();
      connection.invoke.calls.reset();

      store.ask('Who won?');
      answer().subscriber.complete();

      expect(connection.invoke).toHaveBeenCalledWith('Models');
    });
  });
  describe('waiting', () => {
    const forfeit: ChatActionDto = { id: 'a1', description: 'forfeit_season_match(seasonMatchID: 12, side: home)' };

    /** Asks, and the answer is done with `waiting` waiting for the user's answer. */
    async function asking(): Promise<ChatStore> {
      const store = create();
      waiting = forfeit;
      store.ask('Forfeit match 12');
      answer().subscriber.next('This waits for you.');
      answer().subscriber.complete();
      await new Promise(resolve => setTimeout(resolve));
      return store;
    }

    it('shows the change the server keeps for the user once the answer is done', async () => {
      const store = await asking();

      expect(connection.invoke).toHaveBeenCalledWith('Waiting');
      expect(store.waiting()).toEqual(forfeit);
    });

    it('confirms: sends only its ID and the answer, with the conversation, and streams the reply in', async () => {
      const store = await asking();
      waiting = null;

      expect(store.confirm(true)).toBeTrue();

      expect(connection.stream.calls.mostRecent().args as [string, ChatConfirmDto]).toEqual(['Confirm', {
        actionId: 'a1', approved: true, history: [turn(false, 'Forfeit match 12'), turn(true, 'This waits for you.')],
      }]);
      expect(store.waiting()).withContext('answered: no buttons while it runs').toBeNull();
      expect(store.turns().slice(-2)).toEqual([turn(false, CONFIRMED), turn(true, '')]);
      answer().subscriber.next('Done.');
      answer().subscriber.complete();
      await new Promise(resolve => setTimeout(resolve));
      expect(store.turns().at(-1)).toEqual(turn(true, 'Done.'));
      expect(store.waiting()).toBeNull();
    });

    it('cancels', async () => {
      const store = await asking();

      store.confirm(false);

      expect((connection.stream.calls.mostRecent().args as [string, ChatConfirmDto])[1].approved).toBeFalse();
      expect(store.turns().at(-2)).toEqual(turn(false, CANCELLED));
    });

    it('answers nothing when nothing waits, while an answer is on its way, or without a connection', async () => {
      expect(create().confirm(true)).toBeFalse();
      TestBed.resetTestingModule();

      const store = await asking();
      connection.state = HubConnectionState.Reconnecting;
      expect(store.confirm(true)).toBeFalse();
      connection.state = HubConnectionState.Connected;
      store.ask('Something else');
      expect(store.confirm(true)).toBeFalse();
      expect(connection.stream).toHaveBeenCalledTimes(2);
    });

    it('a new conversation drops the change waiting, on the server too', async () => {
      const store = await asking();

      store.clear();

      expect(store.waiting()).toBeNull();
      expect(connection.invoke).toHaveBeenCalledWith('Forget');
    });

    it("a sign-in or out drops it from the page only: the server's is the other user's", async () => {
      const store = await asking();

      TestBed.inject(AuthSession).signedInOrOut();

      expect(store.waiting()).toBeNull();
      expect(connection.invoke).not.toHaveBeenCalledWith('Forget');
    });

    it('logs it when the server fails to drop it', async () => {
      const error = spyOn(console, 'error');
      const store = await asking();
      connection.invoke.and.returnValue(Promise.reject(new Error('down')));

      store.clear();
      await new Promise(resolve => setTimeout(resolve));

      expect(error).toHaveBeenCalledWith('Chat: the waiting change failed to be dropped', jasmine.objectContaining({ message: 'down' }));
    });

    it('loads the change still waiting once the connection is up: after a reload, the buttons are back', async () => {
      waiting = forfeit;
      const store = create();

      (connection.onreconnected.calls.mostRecent().args[0] as () => void)();
      await new Promise(resolve => setTimeout(resolve));

      expect(store.waiting()).toEqual(forfeit);
    });

    it('ignores a waiting change loaded before the next question: the server dropped it', async () => {
      const store = create();
      let answerWaiting!: (action: ChatActionDto | null) => void;
      connection.invoke.and.callFake((method: string) => method === 'Waiting'
        ? new Promise(resolve => { answerWaiting = resolve; })
        : Promise.resolve(models));
      store.ask('Forfeit match 12');
      answer().subscriber.complete();

      store.ask('Something else');
      answerWaiting(forfeit);
      await new Promise(resolve => setTimeout(resolve));

      expect(store.waiting()).toBeNull();
    });

    it('logs it when the waiting change fails to load, and shows none', async () => {
      const error = spyOn(console, 'error');
      const store = create();
      connection.invoke.and.returnValue(Promise.reject(new Error('down')));
      store.ask('Forfeit match 12');
      answer().subscriber.complete();
      await new Promise(resolve => setTimeout(resolve));

      expect(store.waiting()).toBeNull();
      expect(error).toHaveBeenCalledWith('Chat: the waiting change failed to load', jasmine.objectContaining({ message: 'down' }));
    });
  });
});
