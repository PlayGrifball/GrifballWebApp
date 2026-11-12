import { ChangeDetectionStrategy, Component, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { parseUser, UserResponseDto } from '../userResponseDto';
import { DateTime } from 'luxon';
import { toApiDate } from '../../shared/apiDates';
import { RouterLink } from '@angular/router';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { LinkedAccountsComponent } from '../../shared/linkedAccounts/linkedAccounts.component';
import { provideDatePickers } from '../../shared/datePickers';
import { REGIONS } from '../../shared/regions';
import { whileCurrent } from '../../shared/whileCurrent';
import { UserManagementService } from '@api/services/user-management.service';

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
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditUserComponent {
  private userManagement = inject(UserManagementService);

  /** The :userID of the route. Angular keeps this page when only it changes, so a new one resets the form to that user. */
  readonly userID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });

  readonly model = signal<UserResponseDto>({} as UserResponseDto);
  readonly regions = REGIONS;

  /** The region select's value: an ID, or "" for none. */
  setRegion(value: string): void {
    this.patch({ regionID: value ? Number(value) : null });
  }

  // lockoutEnd exactly as the API sent it, and the DateTime made from it. An untouched lockout
  // is sent back as that string: re-serializing the DateTime would round it to milliseconds
  // (the server would see a change on every save) and DateTimeOffset.MaxValue, a permanent
  // lockout, would become year +010000 in zones ahead of UTC, which the server cannot read.
  private loadedLockoutEnd: { raw: string | null, parsed: DateTime | null } = { raw: null, parsed: null };

  /** Form edits replace the model rather than mutating it. */
  patch(changes: Partial<UserResponseDto>): void {
    this.model.update(model => ({ ...model, ...changes }));
  }

  setRole(index: number, hasRole: boolean): void {
    this.model.update(model => ({ ...model, roles: model.roles.map((role, i) => i === index ? { ...role, hasRole } : role) }));
  }

  constructor() {
    effect(() => {
      const userID = this.userID();
      untracked(() => this.load(userID));
    });
  }

  private load(userID: number): void {
    // Nothing of the previous user stays in the form (their linked accounts go with it)
    this.model.set({} as UserResponseDto);
    this.loadedLockoutEnd = { raw: null, parsed: null };
    if (userID === 0) {
      console.log('Invalid user ID');
      return;
    }

    // Fetch from backend
    this.userManagement.getUser({ userID })
      .pipe(whileCurrent(userID, this.userID))
      .subscribe({
        next: (raw) => {
          const user = parseUser(raw);
          if (!user)
            return; // no such user: keep the empty form
          this.loadedLockoutEnd = { raw: raw.lockoutEnd ?? null, parsed: user.lockoutEnd };
          this.model.set(user);
        },
        error: (error) => console.log(error)
      });
  }

  onSubmit() {
    const user = this.model();
    const loaded = this.loadedLockoutEnd.parsed;
    // Same instant as loaded (also when the picker hands back an equal but new DateTime): send the original string.
    const unchanged = user.lockoutEnd === loaded
      || (user.lockoutEnd !== null && loaded !== null && user.lockoutEnd.toMillis() === loaded.toMillis());
    const lockoutEnd = unchanged ? this.loadedLockoutEnd.raw : toApiDate(user.lockoutEnd);
    this.userManagement.editUser({ userID: this.userID(), body: { ...user, lockoutEnd } }).subscribe({
      next: (result) => console.log('Updated user'),
      error: (result) => console.log(result),
    });
  }

}
