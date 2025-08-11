import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import { RescheduleDto } from '../commissioner-dashboard.component';
import { HttpClient } from '@angular/common/http';

interface ProcessRescheduleRequest {
  approved: boolean;
  commissionerNotes?: string;
}

@Component({
  selector: 'app-process-reschedule-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatIconModule
  ],
  templateUrl: './process-reschedule-dialog.component.html',
  styleUrls: ['./process-reschedule-dialog.component.scss']
})
export class ProcessRescheduleDialogComponent {
  decision: 'approve' | 'reject' | null = null;
  commissionerNotes = '';
  processing = false;

  constructor(
    public dialogRef: MatDialogRef<ProcessRescheduleDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public reschedule: RescheduleDto,
    private httpClient: HttpClient
  ) {}

  formatDateTime(dateStr?: string): string {
    if (!dateStr) return 'Not scheduled';
    return new Date(dateStr).toLocaleString();
  }

  async processReschedule(): Promise<void> {
    if (this.decision === null) return;

    const request: ProcessRescheduleRequest = {
      approved: this.decision === 'approve',
      commissionerNotes: this.commissionerNotes.trim() || undefined
    };

    this.processing = true;

    this.httpClient.post(`/api/Reschedule/ProcessReschedule/${this.reschedule.matchRescheduleID}`, request)
    .subscribe({
      next: () => {
        this.dialogRef.close(true); // Signal success
        this.processing = false;
      },
      error: (error) => {
        console.error('Failed to process reschedule:', error);
        // You might want to show an error message here
        this.processing = false;
      }
    });
    
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}