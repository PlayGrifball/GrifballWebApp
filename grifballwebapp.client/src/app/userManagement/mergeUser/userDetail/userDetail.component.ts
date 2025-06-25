import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { UserResponseDto } from '../../userResponseDto';

@Component({
  selector: 'app-user-detail',
  imports: [],
  templateUrl: './userDetail.component.html',
  styleUrl: './userDetail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserDetailComponent {
  user = input<UserResponseDto | undefined>(undefined);
}
