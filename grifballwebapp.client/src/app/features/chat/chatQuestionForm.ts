import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ChatStore, MAX_QUESTION_LENGTH, type AskedFrom } from './chat.store';

/**
 * The question box both chats share (the chat page, Flipyap's corner one): its form (with the question's length limit),
 * Enter to ask, and the thin confirm/stop/clear wrappers around the store. `page` is read fresh with each ask or confirm,
 * so it can change as the user navigates (Flipyap's); the chat page passes none.
 */
export class ChatQuestionForm {
  readonly maxLength = MAX_QUESTION_LENGTH;
  readonly form = new FormGroup({
    question: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(MAX_QUESTION_LENGTH)] }),
  });

  constructor(private readonly store: ChatStore, private readonly page: () => AskedFrom | null = () => null) {}

  /** Asks the question, and empties the box once it is asked. */
  ask(): void {
    if (this.form.valid && this.store.ask(this.form.controls.question.value, this.page()))
      this.form.reset();
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
