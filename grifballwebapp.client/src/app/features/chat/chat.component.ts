import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RouterLink } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { ChatQuestionForm } from './chatQuestionForm';
import { ChatStore } from './chat.store';

/**
 * The chat page: the league's chat, as the Discord bot answers in Discord, on the site. It looks up seasons, teams,
 * players and matches with the same tools, as the signed-in user (signed out, as a Discord user with no linked account).
 * Enter asks, Shift+Enter starts a new line; the answer streams in as it is written.
 */
@Component({
  selector: 'app-chat',
  templateUrl: 'chat.component.html',
  styleUrl: 'chat.component.scss',
  imports: [CurrencyPipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatComponent {
  private readonly store = inject(ChatStore);

  readonly turns = this.store.turns;
  readonly asking = this.store.asking;
  readonly error = this.store.error;
  /** A change the chat asked for, waiting for the user's yes or no: nothing changes until they answer. */
  readonly waiting = this.store.waiting;
  /** Whether a change runs without asking, this conversation; null for someone with no change to make (no toggle). */
  readonly autoApprove = this.store.autoApprove;
  /** This conversation's choice differs from the user's setting: they may keep it as their default. */
  readonly autoApproveUnsaved = computed(() => {
    const on = this.store.autoApprove();
    return on !== null && on !== this.store.models()?.autoApprove;
  });
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

  /** The question box, Enter, and the confirm/stop/clear buttons: shared with Flipyap's chat (no page: this is the whole chat). */
  readonly chat = new ChatQuestionForm(this.store);

  /** The user chose another model: their questions go to it, here and in a Discord DM. */
  choose(model: string): void {
    void this.store.choose(model);
  }

  /** Changes run without asking (or wait) for this conversation. */
  setAutoApprove(on: boolean): void {
    this.store.setAutoApprove(on);
  }

  /** This conversation's choice kept as the user's setting, here and in a Discord DM. */
  saveAutoApprove(): void {
    void this.store.saveAutoApprove();
  }
}
