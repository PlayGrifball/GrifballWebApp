import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { ChatStore, MAX_QUESTION_LENGTH } from './chat.store';

/**
 * The chat page: the league's chat, as the Discord bot answers in Discord, on the site. It looks up seasons, teams,
 * players and matches with the same tools, as the signed-in user (signed out, as a Discord user with no linked account).
 * Enter asks, Shift+Enter starts a new line; the answer streams in as it is written.
 */
@Component({
  selector: 'app-chat',
  templateUrl: 'chat.component.html',
  styleUrl: 'chat.component.scss',
  imports: [CurrencyPipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, RouterLink],
  providers: [ChatStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatComponent {
  private readonly store = inject(ChatStore);

  readonly turns = this.store.turns;
  readonly asking = this.store.asking;
  readonly error = this.store.error;
  readonly signedIn = inject(AccountService).isLoggedIn;
  /** The user's models, when there is a choice to make (more than one they may use); else none, and no picker. */
  readonly picker = computed(() => {
    const models = this.store.models();
    return models && models.models.length > 1 ? models : null;
  });
  /** The user's spend on paid models this month and its limit, when they have a paid model: with a picker or without. */
  readonly spend = computed(() => {
    const models = this.store.models();
    return models?.spent != null && models.limit != null ? { spent: models.spent, limit: models.limit } : null;
  });

  readonly maxLength = MAX_QUESTION_LENGTH;
  readonly form = new FormGroup({
    question: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(MAX_QUESTION_LENGTH)] }),
  });

  /** Asks the question, and empties the box once it is asked. */
  ask(): void {
    if (this.form.valid && this.store.ask(this.form.controls.question.value))
      this.form.reset();
  }

  /** Enter asks (keydown.enter: not with Shift, which starts a new line). */
  onEnter(event: Event): void {
    event.preventDefault();
    this.ask();
  }

  /** The user chose another model: their questions go to it, here and in a Discord DM. */
  choose(model: string): void {
    void this.store.choose(model);
  }

  stop(): void {
    this.store.stop();
  }

  clear(): void {
    this.store.clear();
  }
}
