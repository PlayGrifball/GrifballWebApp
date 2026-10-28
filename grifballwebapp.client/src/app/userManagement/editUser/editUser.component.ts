import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { parseUser, UserResponseDto } from '../userResponseDto';
import { DateTime } from 'luxon';
import { toApiDate } from '../../shared/apiDates';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatCheckboxModule } from '@angular/material/checkbox';

@Component({
    selector: 'app-edit-user',
    imports: [
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        MtxDatetimepickerModule,
        MatCheckboxModule
    ],
    templateUrl: './editUser.component.html',
    styleUrl: './editUser.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditUserComponent {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  readonly model = signal<UserResponseDto>({} as UserResponseDto);

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

  ngOnInit(): void {
    const userID = Number(this.route.snapshot.paramMap.get('userID'));

    if (userID === 0) {
      console.log('Invalid user ID');
      return;
    }

    // Fetch from backend
    this.http.get<any>('/api/usermanagement/getuser/' + userID)
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
    this.http.post('/api/usermanagement/edituser/', { ...user, lockoutEnd }).subscribe({
      next: (result) => console.log('Updated user'),
      error: (result) => console.log(result),
    });
  }

}
