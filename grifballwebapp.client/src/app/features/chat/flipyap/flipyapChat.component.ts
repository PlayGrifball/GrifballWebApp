import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, afterRenderEffect, effect, inject, output, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { ChatStore, MAX_QUESTION_LENGTH, type AskedFrom } from '../chat.store';

/**
 * Flipyap's chat, small enough for a corner of any page: the league's chat (the same as the chat page, its conversation
 * too) with the questions, the answer streaming in, and a change waiting for the user's yes or no. The model picker and
 * the run-without-asking setting stay on the chat page, which it links to. It has its own store: the hub connects when
 * it is made.
 */
@Component({
  selector: 'app-flipyap-chat',
  templateUrl: 'flipyapChat.component.html',
  styleUrl: 'flipyapChat.component.scss',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [ChatStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FlipyapChatComponent {
  private readonly store = inject(ChatStore);
  private readonly router = inject(Router);
  private readonly title = inject(Title);

  readonly turns = this.store.turns;
  readonly asking = this.store.asking;
  readonly error = this.store.error;
  readonly waiting = this.store.waiting;
  readonly signedIn = inject(AccountService).isLoggedIn;

  /** The user closed the chat (its button, or Escape on the pet). */
  readonly closed = output<void>();
  /** Whether Flipyap is answering: the pet moves his mouth while he does. */
  readonly talking = output<boolean>();

  readonly maxLength = MAX_QUESTION_LENGTH;
  readonly form = new FormGroup({
    question: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(MAX_QUESTION_LENGTH)] }),
  });

  private readonly log = viewChild<ElementRef<HTMLElement>>('log');
  private readonly questionBox = viewChild<ElementRef<HTMLTextAreaElement>>('questionBox');

  constructor() {
    effect(() => this.talking.emit(this.asking()));
    // The newest message is the one in view, as it streams in
    afterRenderEffect(() => {
      this.turns();
      const log = this.log()?.nativeElement;
      if (log)
        log.scrollTop = log.scrollHeight;
    });
    afterNextRender(() => this.focus());
  }

  /** Puts the cursor in the question box (the chat was just opened). */
  focus(): void {
    this.questionBox()?.nativeElement.focus();
  }

  ask(): void {
    if (this.form.valid && this.store.ask(this.form.controls.question.value, this.page()))
      this.form.reset();
  }

  /** The page Flipyap stands on, as the model is told it: the address without its query and fragment, and the page's title. */
  private page(): AskedFrom {
    return { path: this.router.url.split(/[?#]/)[0], title: this.title.getTitle() };
  }

  /** Enter asks (keydown.enter: not with Shift, which starts a new line). */
  onEnter(event: Event): void {
    event.preventDefault();
    this.ask();
  }

  confirm(approved: boolean): void {
    this.store.confirm(approved);
  }

  stop(): void {
    this.store.stop();
  }

  clear(): void {
    this.store.clear();
  }
}
