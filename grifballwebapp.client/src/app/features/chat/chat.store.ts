import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HubConnectionState, type HubConnection, type ISubscription } from '@microsoft/signalr';
import type { ChatTurnDto } from '@api/models';
import { AuthSession } from '@core/auth/authSession.service';
import { hubLifecycle } from '@core/signalr/hubLifecycle';
import { getHubProxyFactory } from '@signalr/TypedSignalR.Client';

/** The chat's hub (ChatHub): it streams each answer as the model writes it. */
export const CHAT_HUB_URL = 'hub/ChatHub';

/** How many earlier messages the server reads with a question, and how much of each (GrifChat): no more is sent. */
export const HISTORY_LIMIT = 25;
export const MAX_HISTORY_MESSAGE_LENGTH = 4000;
/** The longest question the server takes (GrifChat.MaxQuestionLength, a Discord message's limit). */
export const MAX_QUESTION_LENGTH = 2000;

/** Where the tab keeps its conversation: the server keeps none, and it goes with the tab or a sign-in or out. */
export const CHAT_STORAGE_KEY = 'grif-chat';

export const NOT_CONNECTED = 'The chat is still connecting: try again in a moment.';
export const FAILED = "The chat couldn't answer just now: try again later.";

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
  private readonly connection: HubConnection | null = isPlatformBrowser(inject(PLATFORM_ID))
    ? hubLifecycle(CHAT_HUB_URL, { name: 'Chat hub' })
    : null;

  private readonly turnsState = signal<ChatTurnDto[]>(this.connection ? load() : []);
  /** The conversation, oldest first; while an answer streams in, its last message is that answer so far. */
  readonly turns = this.turnsState.asReadonly();

  private readonly askingState = signal(false);
  /** An answer is on its way. */
  readonly asking = this.askingState.asReadonly();

  private readonly errorState = signal<string | null>(null);
  /** Why the last question got no (whole) answer. */
  readonly error = this.errorState.asReadonly();

  private answer: ISubscription<string> | null = null;

  constructor() {
    // Another user's conversation isn't theirs to see or continue
    inject(AuthSession).changed$.pipe(takeUntilDestroyed()).subscribe(() => this.clear());
    inject(DestroyRef).onDestroy(() => this.answer?.dispose());
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
    const history = this.turns().slice(-HISTORY_LIMIT).map(turn => ({ fromBot: turn.fromBot, text: turn.text.slice(0, MAX_HISTORY_MESSAGE_LENGTH) }));
    this.errorState.set(null);
    this.askingState.set(true);
    this.change(turns => [...turns, { fromBot: false, text }, { fromBot: true, text: '' }]);
    this.answer = getHubProxyFactory('IChatHubServer').createHubProxy(this.connection).ask({ question: text, history }).subscribe({
      next: chunk => this.change(turns => [...turns.slice(0, -1), { fromBot: true, text: turns.at(-1)!.text + chunk }]),
      error: (error: unknown) => {
        this.errorState.set(chatErrorText(error));
        this.done();
      },
      complete: () => this.done(),
    });
    return true;
  }

  /** Stops the answer on its way (the server stops writing it), keeping what came of it. */
  stop(): void {
    if (!this.asking())
      return;
    this.answer?.dispose();
    this.done();
  }

  /** A new conversation: the answer on its way stopped, and the questions and answers so far gone. */
  clear(): void {
    this.stop();
    this.errorState.set(null);
    this.change(() => []);
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
