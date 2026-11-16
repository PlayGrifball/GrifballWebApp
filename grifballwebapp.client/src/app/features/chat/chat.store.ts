import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HubConnectionState, type HubConnection, type IStreamResult, type ISubscription } from '@microsoft/signalr';
import type { ChatActionDto, ChatModelsDto, ChatTurnDto } from '@api/models';
import { AuthSession } from '@core/auth/authSession.service';
import { hubLifecycle } from '@core/signalr/hubLifecycle';
import { AppLog } from '@core/telemetry/appLog';
import { getHubProxyFactory } from '@signalr/TypedSignalR.Client';
import type { IChatHubServer } from '@signalr/TypedSignalR.Client/GrifballWebApp.Server.Chat';

/** The chat's hub (ChatHub): it streams each answer as the model writes it. */
export const CHAT_HUB_URL = 'hub/ChatHub';

/** How many earlier messages the server reads with a question, and how much of each (GrifChat): no more is sent. */
export const HISTORY_LIMIT = 25;
export const MAX_HISTORY_MESSAGE_LENGTH = 4000;
/** The longest question the server takes (GrifChat.MaxQuestionLength, a Discord message's limit). */
export const MAX_QUESTION_LENGTH = 2000;

/** Where the tab keeps its conversation: the server keeps none, and it goes with the tab or a sign-in or out. */
export const CHAT_STORAGE_KEY = 'grif-chat';

/**
 * How long after a stop the spend is loaded again: the server saves a stopped paid answer's cost only once its stream has
 * wound down, a moment after the stop.
 */
export const STOP_SPEND_RELOAD_MS = 2000;

export const NOT_CONNECTED ='The chat is still connecting: try again in a moment.';
export const FAILED = "The chat couldn't answer just now: try again later.";

/** What the conversation shows for the user's answer to a change waiting for it. */
export const CONFIRMED = 'Yes, do it.';
export const CANCELLED = 'No, cancel it.';

/** What SignalR puts before a HubException's message, the reason the server gives the browser. */
const HUB_EXCEPTION = 'HubException: ';

/**
 * What to show for a failed answer: the server's reason when it gave one (a HubException's message: a question asked too
 * often, an answer that took too long), else that it failed.
 */
export function chatErrorText(error: unknown): string {
  const message = error instanceof Error ? error.message : '';
  const at = message.indexOf(HUB_EXCEPTION);
  return at >= 0 && message.length > at + HUB_EXCEPTION.length ? message.slice(at + HUB_EXCEPTION.length) : FAILED;
}

/** The conversation the tab kept, or none when there is none or it isn't one. */
function load(): ChatTurnDto[] {
  try {
    const kept: unknown = JSON.parse(sessionStorage.getItem(CHAT_STORAGE_KEY) ?? '[]');
    return Array.isArray(kept) && kept.every(isTurn) ? kept : [];
  } catch {
    return [];
  }
}

function isTurn(value: unknown): value is ChatTurnDto {
  const turn = value as Partial<ChatTurnDto> | null;
  return typeof turn?.fromBot === 'boolean' && typeof turn.text === 'string';
}

function save(turns: ChatTurnDto[]): void {
  try {
    sessionStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(turns));
  } catch {
    // Storage full or off: the conversation lasts as long as the page
  }
}

/**
 * The chat page's conversation with the league's chat, the Discord bot's on the site: the questions and answers so far
 * (kept in the tab's sessionStorage, and sent with each question for the model's context), the answer streaming in, and
 * why the last question failed. The hub connection is the user's (their session cookie): signed out, the chat answers as
 * it does a Discord user with no linked account. A sign-in or out starts a new conversation. Only in the browser.
 */
@Injectable()
export class ChatStore {
  private readonly log = inject(AppLog);
  private readonly connection: HubConnection | null = isPlatformBrowser(inject(PLATFORM_ID))
    // Each time it comes up (as whoever is signed in now): the models they may use, and a change still waiting for their answer
    ? hubLifecycle(CHAT_HUB_URL, {
      name: 'Chat hub',
      connected: () => {
        void this.loadModels(this.connection!);
        void this.loadWaiting(this.connection!);
      },
    })
    : null;

  private readonly modelsState = signal<ChatModelsDto | null>(null);
  /**
   * The models the user may use, the one their questions go to, and their spend on paid ones this month: null until the
   * connection is up.
   */
  readonly models = this.modelsState.asReadonly();

  private readonly conversationAutoApproveState = signal<boolean | null>(null);
  /**
   * Whether a change the chat makes runs without asking, this conversation: the user's choice for it, else their setting; null when
   * they have no change to make (no setting).
   */
  readonly autoApprove = computed(() => {
    const saved = this.models()?.autoApprove;
    return saved == null ? null : (this.conversationAutoApproveState() ?? saved);
  });

  private readonly turnsState = signal<ChatTurnDto[]>(this.connection ? load() : []);
  /** The conversation, oldest first; while an answer streams in, its last message is that answer so far. */
  readonly turns = this.turnsState.asReadonly();

  private readonly askingState = signal(false);
  /** An answer is on its way. */
  readonly asking = this.askingState.asReadonly();

  private readonly errorState = signal<string | null>(null);
  /** Why the last question got no (whole) answer. */
  readonly error = this.errorState.asReadonly();

  private readonly waitingState = signal<ChatActionDto | null>(null);
  /**
   * The change the chat asked to make, waiting for the user's answer (confirm), shown with what it does; null when none.
   * The server keeps it: the page sends back only its ID and the answer.
   */
  readonly waiting = this.waitingState.asReadonly();

  private answer: ISubscription<string> | null = null;
  /** Counts the answers started and conversations cleared: a waiting change loaded before the latest is out of date. */
  private generation = 0;
  /** The spend's reload after a stop, waiting for the server to save the stopped answer's cost. */
  private spendReload: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // Another user's conversation isn't theirs to see or continue
    inject(AuthSession).changed$.pipe(takeUntilDestroyed()).subscribe(() => this.reset());
    inject(DestroyRef).onDestroy(() => {
      this.answer?.dispose();
      clearTimeout(this.spendReload);
    });
  }

  /**
   * Asks `question` (trimmed), with the conversation so far: its answer streams into the conversation's last message.
   * False when nothing was asked: no question, an answer still on its way, or no connection yet (the error says so).
   */
  ask(question: string): boolean {
    const text = question.trim();
    if (!text || this.asking() || !this.connection)
      return false;
    if (this.connection.state !== HubConnectionState.Connected) {
      this.errorState.set(NOT_CONNECTED);
      return false;
    }
    const connection = this.connection;
    // The model the picker shows: the server would choose the same, unless the user chose another elsewhere meanwhile
    const model = this.models()?.selected;
    const history = this.history();
    const autoApprove = this.conversationAutoApproveState();
    this.stream(connection, text, hub => hub.ask({ question: text, history, model: model ?? null, autoApprove }));
    return true;
  }

  /**
   * The user's answer to the change waiting for it: yes does it, no cancels it. The chat's reply streams in as an answer does,
   * after a line saying what they chose.
   */
  confirm(approved: boolean): boolean {
    const waiting = this.waiting();
    if (!waiting || this.asking() || this.connection?.state !== HubConnectionState.Connected)
      return false;
    const history = this.history();
    this.stream(this.connection, approved ? CONFIRMED : CANCELLED,
      hub => hub.confirm({ actionId: waiting.id, approved, history }));
    return true;
  }

  /** The conversation so far, as much as the server reads. */
  private history(): ChatTurnDto[] {
    return this.turns().slice(-HISTORY_LIMIT).map(turn => ({ fromBot: turn.fromBot, text: turn.text.slice(0, MAX_HISTORY_MESSAGE_LENGTH) }));
  }

  /** `said` added to the conversation, then the answer `start` streams in; then what waits for the user, if anything does. */
  private stream(connection: HubConnection, said: string, start: (hub: IChatHubServer) => IStreamResult<string>): void {
    this.generation++;
    this.errorState.set(null);
    this.waitingState.set(null);
    this.askingState.set(true);
    this.change(turns => [...turns, { fromBot: false, text: said }, { fromBot: true, text: '' }]);
    this.answer = start(getHubProxyFactory('IChatHubServer').createHubProxy(connection)).subscribe({
      next: chunk => this.change(turns => [...turns.slice(0, -1), { fromBot: true, text: turns.at(-1)!.text + chunk }]),
      error: (error: unknown) => {
        const text = chatErrorText(error);
        // A refusal with the server's reason is the server's to log
        if (text === FAILED)
          this.log.error('Chat: the answer failed', error);
        this.errorState.set(text);
        this.done();
        // A paid answer is charged whether it was whole or not
        this.loadSpend(connection);
      },
      complete: () => {
        this.done();
        this.loadSpend(connection);
        void this.loadWaiting(connection);
      },
    });
  }

  /**
   * The user chose `model` (its ID) for their questions, here and in a Discord DM: the server keeps it. Refused (a model
   * they may not use), the error says why.
   */
  async choose(model: string): Promise<void> {
    if (!this.connection || model === this.models()?.selected)
      return;
    try {
      this.modelsState.set(await getHubProxyFactory('IChatHubServer').createHubProxy(this.connection).choose(model));
    } catch (error: unknown) {
      this.errorState.set(chatErrorText(error));
      // The picker already shows the refused model: the user's models again, a new value, put it back
      await this.loadModels(this.connection);
    }
  }

  /** Whether changes run without asking, this conversation only: their setting is unchanged. */
  setAutoApprove(on: boolean): void {
    this.conversationAutoApproveState.set(on);
  }

  /** This conversation's choice of whether changes run without asking, kept as their setting (here and in a Discord DM). */
  async saveAutoApprove(): Promise<void> {
    const on = this.autoApprove();
    if (!this.connection || on === null)
      return;
    try {
      this.modelsState.set(await getHubProxyFactory('IChatHubServer').createHubProxy(this.connection).setAutoApprove(on));
      this.conversationAutoApproveState.set(null);
    } catch (error: unknown) {
      this.errorState.set(chatErrorText(error));
    }
  }

  /** The change waiting for the user's answer, from the server; none when it can't say. */
  private async loadWaiting(connection: HubConnection): Promise<void> {
    const generation = this.generation;
    try {
      const waiting = await getHubProxyFactory('IChatHubServer').createHubProxy(connection).waiting();
      // A question asked (or the conversation cleared) since: the server dropped it
      if (generation === this.generation)
        this.waitingState.set(waiting);
    } catch (error: unknown) {
      this.log.error('Chat: the waiting change failed to load', error);
    }
  }

  /** What the last answer cost, when the user has a paid model: their spend this month again. */
  private loadSpend(connection: HubConnection): void {
    if (this.models()?.spent != null)
      void this.loadModels(connection);
  }

  /** The user's models from the server; left as they were when it can't say. */
  private async loadModels(connection: HubConnection): Promise<void> {
    try {
      this.modelsState.set(await getHubProxyFactory('IChatHubServer').createHubProxy(connection).models());
    } catch (error: unknown) {
      this.log.error('Chat: the models failed to load', error);
    }
  }

  /** Stops the answer on its way (the server stops writing it), keeping what came of it. */
  stop(): void {
    if (!this.asking())
      return;
    this.answer?.dispose();
    this.done();
    // A paid answer stopped is charged what it used so far, saved once it has wound down: its spend, a moment later
    const connection = this.connection!;
    clearTimeout(this.spendReload);
    this.spendReload = setTimeout(() => this.loadSpend(connection), STOP_SPEND_RELOAD_MS);
  }

  /**
   * A new conversation: the answer on its way stopped, the questions and answers so far gone, and a change waiting for their
   * answer dropped on the server too (else a yes elsewhere could still run it).
   */
  clear(): void {
    if (this.waiting() && this.connection?.state === HubConnectionState.Connected)
      void this.forget(this.connection);
    this.reset();
  }

  /** The conversation gone from the page (another user's, once they sign in or out: the server's waiting change is theirs). */
  private reset(): void {
    this.generation++;
    this.conversationAutoApproveState.set(null);
    this.stop();
    this.errorState.set(null);
    this.waitingState.set(null);
    this.change(() => []);
  }

  private async forget(connection: HubConnection): Promise<void> {
    try {
      await getHubProxyFactory('IChatHubServer').createHubProxy(connection).forget();
    } catch (error: unknown) {
      this.log.error('Chat: the waiting change failed to be dropped', error);
    }
  }

  private done(): void {
    this.answer = null;
    this.askingState.set(false);
    // An answer that never began (refused, failed or stopped at once) leaves no empty message
    this.change(turns => {
      const last = turns.at(-1);
      return last?.fromBot && !last.text ? turns.slice(0, -1) : turns;
    });
  }

  private change(update: (turns: ChatTurnDto[]) => ChatTurnDto[]): void {
    this.turnsState.update(update);
    if (this.connection)
      save(this.turnsState());
  }
}
