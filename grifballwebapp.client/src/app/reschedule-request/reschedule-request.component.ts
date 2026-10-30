import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HttpClient } from '@angular/common/http';
import { Location } from '@angular/common';

interface RescheduleRequest {
  seasonMatchID: number;
  newScheduledTime?: string;
  reason: string;
}

@Component({
  selector: 'app-reschedule-request',
  standalone: true,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSnackBarModule
  ],
  templateUrl: './reschedule-request.component.html',
  styleUrls: ['./reschedule-request.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RescheduleRequestComponent implements OnInit {
  private route = inject(ActivatedRoute);
  location = inject(Location);
  private httpClient = inject(HttpClient);
  private snackBar = inject(MatSnackBar);

  seasonMatchID: number = 0;
  newDate?: Date;
  newTime: string = '';
  reason: string = '';
  readonly submitting = signal(false);

  ngOnInit(): void {
    this.seasonMatchID = Number(this.route.snapshot.paramMap.get('seasonMatchID'));
  }

  submitRescheduleRequest(): void {
    if (!this.reason.trim()) {
      this.snackBar.open('Please provide a reason for the reschedule', 'Close', { duration: 3000 });
      return;
    }

    this.submitting.set(true);

    let newScheduledTime: string | undefined;
    if (this.newDate && this.newTime) {
      const [hours, minutes] = this.newTime.split(':').map(Number);
      const dateTime = new Date(this.newDate);
      dateTime.setHours(hours, minutes, 0, 0);
      newScheduledTime = dateTime.toISOString();
    }

    const request: RescheduleRequest = {
      seasonMatchID: this.seasonMatchID,
      newScheduledTime,
      reason: this.reason.trim()
    };

    this.httpClient.post('/api/Reschedule/RequestReschedule', request)
    .subscribe({
      next: () => {
        this.snackBar.open('Reschedule request submitted successfully!', 'Close', { duration: 5000 });
        this.location.back();
        this.submitting.set(false);
      },
      error: (error) => {
        console.error('Failed to submit reschedule request:', error);
        this.snackBar.open('Failed to submit reschedule request. Please try again.', 'Close', { duration: 5000 });
        this.submitting.set(false);
      }
    });
  }
}