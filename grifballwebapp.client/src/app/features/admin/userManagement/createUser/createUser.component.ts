import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { CreateUserDto } from './createUserDto';
import { CreateUserStore } from './createUser.store';

@Component({
    selector: 'app-create-user',
    imports: [
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent
    ],
    templateUrl: './createUser.component.html',
    styleUrl: './createUser.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [CreateUserStore],
})
export class CreateUserComponent {
  private readonly store = inject(CreateUserStore);

  model: CreateUserDto = {} as CreateUserDto;

  onSubmit(): void {
    this.store.create(this.model);
  }
}
