import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ApiClientService } from '../api/apiClient.service';
import { SignupResponseDto } from '../api/dtos/signupResponseDto';
import { ActivatedRoute } from '@angular/router';
import { MatTableModule } from '@angular/material/table';

@Component({
    selector: 'app-signups',
    imports: [
        CommonModule,
        MatTableModule,
    ],
    templateUrl: './signups.component.html',
    styleUrl: './signups.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager
})
export class SignupsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);

  public signUps: SignupResponseDto[] = [];
  public displayedColumns: string[] = ['personName', 'timestamp', 'teamName', 'willCaptain', 'requiresAssistanceDrafting'];
  seasonID: number = 0;

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID = result;
            this.getSignups();
          },
        });
    }
    else {
      this.getSignups();
    }
  }

  getSignups(): void {
    this.api.getSignups(this.seasonID).subscribe({
      next: (result) => this.signUps = result,
      error: (error) => console.error(error),
    });
  }
}
