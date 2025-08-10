import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiClientService } from '../api/apiClient.service';

interface RescheduleRequest {
  seasonMatchID: number;
  newScheduledTime?: string;
  reason: string;
}

@Component({
  selector: 'app-reschedule-request',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
    MatSnackBarModule
  ],
  templateUrl: './reschedule-request.component.html',
  styleUrls: ['./reschedule-request.component.scss']
})
export class RescheduleRequestComponent implements OnInit {
  seasonMatchID: number = 0;
  newDate?: Date;
  newTime: string = '';
  reason: string = '';
  submitting = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiClient: ApiClientService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.seasonMatchID = Number(this.route.snapshot.paramMap.get('seasonMatchID'));
    if (!this.seasonMatchID) {
      this.router.navigate(['/']);
    }
  }

  async submitRescheduleRequest(): Promise<void> {
    if (!this.reason.trim()) {
      this.snackBar.open('Please provide a reason for the reschedule', 'Close', { duration: 3000 });
      return;
    }

    this.submitting = true;

    try {
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

      await this.apiClient.post('Reschedule/RequestReschedule', request);
      
      this.snackBar.open('Reschedule request submitted successfully!', 'Close', { duration: 5000 });
      this.router.navigate(['/']);
    } catch (error) {
      console.error('Failed to submit reschedule request:', error);
      this.snackBar.open('Failed to submit reschedule request. Please try again.', 'Close', { duration: 5000 });
    } finally {
      this.submitting = false;
    }
  }
}