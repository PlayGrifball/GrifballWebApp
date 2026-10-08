import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { UserResponseDto } from '../userResponseDto';
import { SearchBoxComponent } from '../../shared/searchBox/searchBox.component';
import { UserDetailComponent } from './userDetail/userDetail.component';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';

@Component({
  selector: 'app-merge-user',
  imports: [
    SearchBoxComponent,
    UserDetailComponent,
    MatSlideToggleModule,
    FormsModule,
    MatButton,
  ],
  templateUrl: './mergeUser.component.html',
  styleUrl: './mergeUser.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MergeUserComponent {

  fromInput = input<number>(0);
  toInput = input<number>(0);

  // An undefined URL keeps the resource idle, so no request is made until an ID is chosen.
  mergeFromId = signal<number>(0);
  mergeFrom = httpResource<UserResponseDto>(() => this.mergeFromId() ? '/api/usermanagement/getuser/' + this.mergeFromId() : undefined);

  mergeToId = signal<number>(0);
  mergeTo = httpResource<UserResponseDto>(() => this.mergeToId() ? '/api/usermanagement/getuser/' + this.mergeToId() : undefined);

  // value() throws while a resource is in the error state (Angular 20+), so
  // the template reads these instead and shows resource.error() separately.
  mergeFromUser = computed(() => this.mergeFrom.hasValue() ? this.mergeFrom.value() : undefined);
  mergeToUser = computed(() => this.mergeTo.hasValue() ? this.mergeTo.value() : undefined);

  transferDiscord = signal(true);
  transferXbox = signal(true);
  transferTeamPlayers = signal(true);
  transferExperiences = signal(true);
  transferSignups = signal(true);
  deleteMergeFrom = signal(true);

  constructor(private http: HttpClient) {
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
    const dto = {
      mergeToId: this.mergeToId(),
      mergeFromId: this.mergeFromId(),
      mergeOptions: {
        transferDiscord: this.transferDiscord(),
        transferXbox: this.transferXbox(),
        transferTeamPlayers: this.transferTeamPlayers(),
        transferExperiences: this.transferExperiences(),
        transferSignups: this.transferSignups(),
        deleteMergeFrom: this.deleteMergeFrom(),
      },
    };

    this.http.post('/api/usermanagement/MergeUser/', dto).subscribe({
      next: (result) => console.log('Merged user'),
      error: (result) => console.log(result),
    });

  }
}
