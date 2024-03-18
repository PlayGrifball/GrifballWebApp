import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';
import { FormsModule, NgForm } from '@angular/forms';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { ErrorMessageComponent } from '../validation/errorMessage.component';

@Component({
  selector: 'app-season-edit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    MatDatepickerModule,
  ],
  templateUrl: './seasonEdit.component.html',
  styleUrl: './seasonEdit.component.scss'
})
export class SeasonEditComponent implements OnInit {
  @ViewChild('seasonForm') registerForm!: NgForm;

  model: SeasonDto = {} as SeasonDto;

  constructor(private route: ActivatedRoute, private api: ApiClientService) {}

  ngOnInit(): void {
    const seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    // Fetch from backend
    if (seasonID > 0) {
      this.api.getSeason(seasonID)
        .subscribe({
          next: (result) => this.model = result,
        });
      return;
    }

    // New record
  }

  onSubmit() {
    if (!this.registerForm.valid) {
      return;
    }

    this.api.upsertSeason(this.model).subscribe({
      next: (result) => this.model.seasonID = result,
      });
  }
}
