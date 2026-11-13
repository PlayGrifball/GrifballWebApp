import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import type { ProcessRescheduleDto, RescheduleDto } from '@api/models';
import { RescheduleService } from '@api/services/reschedule.service';
import { errorText } from '@shared/errorText';

@Component({
  selector: 'app-process-reschedule-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatIconModule
  ],
  templateUrl: './processRescheduleDialog.component.html',
  styleUrls: ['./processRescheduleDialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProcessRescheduleDialogComponent {
  dialogRef = inject<MatDialogRef<ProcessRescheduleDialogComponent>>(MatDialogRef);
  reschedule = inject<RescheduleDto>(MAT_DIALOG_DATA);
  private rescheduleService = inject(RescheduleService);

  /** False when the match can no longer move: only a rejection is offered, chosen already. */
  readonly canApprove = this.reschedule.canApprove !== false;
  readonly decision = signal<'approve' | 'reject' | null>(this.canApprove ? null : 'reject');
  readonly commissionerNotes = signal('');
  readonly processing = signal(false);
  /** Why the server refused the decision; shown in the dialog, which stays open to try again. */
  readonly error = signal<string | null>(null);

  formatDateTime(dateStr?: string | null): string {
    if (!dateStr) return 'Not scheduled';
    return new Date(dateStr).toLocaleString();
  }

  processReschedule(): void {
    if (this.decision() === null) return;

    const request: ProcessRescheduleDto = {
      approved: this.decision() === 'approve',
      commissionerNotes: this.commissionerNotes().trim() || null
    };

    this.processing.set(true);
    this.error.set(null);

    this.rescheduleService.processReschedule({ rescheduleID: this.reschedule.matchRescheduleID, body: request })
    .subscribe({
      next: () => {
        this.dialogRef.close(true); // Signal success
        this.processing.set(false);
      },
      error: (error) => {
        // The server says why (e.g. "This match already has a result."); otherwise a general message
        this.error.set(errorText(error, 'The request could not be processed. Try again.'));
        this.processing.set(false);
      }
    });
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}