import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import type { ProcessRescheduleDto, RescheduleDto } from '@api/models';
import { ProcessRescheduleDialogStore } from './processRescheduleDialog.store';

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
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ProcessRescheduleDialogStore],
})
export class ProcessRescheduleDialogComponent {
  dialogRef = inject<MatDialogRef<ProcessRescheduleDialogComponent>>(MatDialogRef);
  reschedule = inject<RescheduleDto>(MAT_DIALOG_DATA);
  private readonly store = inject(ProcessRescheduleDialogStore);

  /** False when the match can no longer move: only a rejection is offered, chosen already. */
  readonly canApprove = this.reschedule.canApprove !== false;
  readonly decision = signal<'approve' | 'reject' | null>(this.canApprove ? null : 'reject');
  readonly commissionerNotes = signal('');
  readonly processing = this.store.processing;
  /** Why the server refused the decision; shown in the dialog, which stays open to try again. */
  readonly error = this.store.error;

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

    this.store.process(this.reschedule.matchRescheduleID, request, () => this.dialogRef.close(true));
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}