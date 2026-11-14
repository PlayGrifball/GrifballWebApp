import { ChangeDetectionStrategy, Component, inject, input, linkedSignal, numberAttribute } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { UserResponseDto } from '../userResponseDto';
import { toApiDate } from '@shared/apiDates';
import { RouterLink } from '@angular/router';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { LinkedAccountsComponent } from '@shared/linkedAccounts/linkedAccounts.component';
import { provideDatePickers } from '@shared/datePickers';
import { REGIONS } from '@shared/regions';
import { EditUserStore } from './editUser.store';

@Component({
    selector: 'app-edit-user',
    imports: [
        RouterLink,
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        MtxDatetimepickerModule,
        MatCheckboxModule,
        LinkedAccountsComponent
    ],
    templateUrl: './editUser.component.html',
    styleUrl: './editUser.component.scss',
    providers: [provideDatePickers(), EditUserStore],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditUserComponent {
  private readonly store = inject(EditUserStore);

  /** The :userID of the route. Angular keeps this page when only it changes, so a new one resets the form to that user. */
  readonly userID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });

  /** The form: the user once they arrive, empty before (their linked accounts go with it); empty again for each user. */
  readonly model = linkedSignal<{ userID: number, user: UserResponseDto | null }, UserResponseDto>({
    source: () => ({ userID: this.userID(), user: this.store.loaded()?.user ?? null }),
    computation: ({ user }) => user ?? {} as UserResponseDto,
  });
  readonly regions = REGIONS;

  /** The region select's value: an ID, or "" for none. */
  setRegion(value: string): void {
    this.patch({ regionID: value ? Number(value) : null });
  }

  /** Form edits replace the model rather than mutating it. */
  patch(changes: Partial<UserResponseDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  setRole(index: number, hasRole: boolean): void {
    this.model.update(model => ({ ...model, roles: model.roles.map((role, i) => i === index ? { ...role, hasRole } : role) }));
  }

  constructor() {
    this.store.userID.use(this.userID);
  }

  onSubmit() {
    const user = this.model();
    // lockoutEnd exactly as the API sent it, when it is unchanged. Re-serializing the DateTime would round it to
    // milliseconds (the server would see a change on every save) and DateTimeOffset.MaxValue, a permanent lockout,
    // would become year +010000 in zones ahead of UTC, which the server cannot read.
    const loadedUser = this.store.loaded();
    const loaded = loadedUser?.user.lockoutEnd ?? null;
    // Same instant as loaded (also when the picker hands back an equal but new DateTime): send the original string.
    const unchanged = user.lockoutEnd === loaded
      || (user.lockoutEnd !== null && loaded !== null && user.lockoutEnd.toMillis() === loaded.toMillis());
    const lockoutEnd = unchanged ? loadedUser?.rawLockoutEnd ?? null : toApiDate(user.lockoutEnd);
    this.store.save(this.userID(), { ...user, lockoutEnd });
  }
}
