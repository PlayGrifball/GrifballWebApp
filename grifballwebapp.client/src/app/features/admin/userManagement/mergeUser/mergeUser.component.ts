import { ChangeDetectionStrategy, Component, effect, input, signal, inject } from '@angular/core';
import { SearchBoxComponent } from '@shared/searchBox/searchBox.component';
import { UserDetailComponent } from './userDetail/userDetail.component';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import type { MergeRequest } from '@api/models';
import { MergeUserStore } from './mergeUser.store';
import { SkeletonComponent } from '@shared/skeleton/skeleton.component';

@Component({
  selector: 'app-merge-user',
  imports: [
    SearchBoxComponent,
    UserDetailComponent,
    MatSlideToggleModule,
    FormsModule,
    MatButton,
    SkeletonComponent,
  ],
  templateUrl: './mergeUser.component.html',
  styleUrl: './mergeUser.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MergeUserStore],
})
export class MergeUserComponent {
  private readonly store = inject(MergeUserStore);

  fromInput = input<number>(0);
  toInput = input<number>(0);

  /** The users chosen by ID (0: none). */
  mergeFromId = signal<number>(0);
  mergeToId = signal<number>(0);

  mergeFromUser = this.store.fromUser;
  mergeToUser = this.store.toUser;
  /** A lookup failed; no such user (404) is not a failure: it shows as "User not found.". */
  mergeFromFailed = this.store.fromFailed;
  readonly mergeFromLoading = this.store.fromLoading;
  readonly mergeToLoading = this.store.toLoading;
  mergeToFailed = this.store.toFailed;

  transferDiscord = signal(true);
  transferLogins = signal(true);
  transferXbox = signal(true);
  transferTeamPlayers = signal(true);
  transferExperiences = signal(true);
  transferSignups = signal(true);
  deleteMergeFrom = signal(true);

  // Disables the button while a merge is in flight, so a double click cannot send a second request.
  submitting = this.store.submitting;
  result = this.store.result;

  constructor() {
    this.store.fromID.use(this.mergeFromId);
    this.store.toID.use(this.mergeToId);
    effect(() => {
      const x = this.fromInput();
      if (x)
        this.mergeFromId.set(x);
    });

    effect(() => {
      const x = this.toInput();
      if (x)
        this.mergeToId.set(x);
    });
  }

  submit() {
    const dto: MergeRequest = {
      mergeToId: this.mergeToId(),
      mergeFromId: this.mergeFromId(),
      mergeOptions: {
        transferDiscord: this.transferDiscord(),
        transferLogins: this.transferLogins(),
        transferXbox: this.transferXbox(),
        transferTeamPlayers: this.transferTeamPlayers(),
        transferExperiences: this.transferExperiences(),
        transferSignups: this.transferSignups(),
        deleteMergeFrom: this.deleteMergeFrom(),
      },
    };

    this.store.merge(dto);
  }
}
