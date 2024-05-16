import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { SeasonMatchPageDto } from './seasonMatchPageDto';
import { MatGridListModule } from '@angular/material/grid-list';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule, NgForm } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { RegexMatchValidValidatorDirective } from '../../validation/directives/regexMatchValidValidatorDirective.directive';

@Component({
  selector: 'app-season-match',
  standalone: true,
  imports: [
    CommonModule,
    MatGridListModule,
    RouterModule,
    FormsModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    ErrorMessageComponent,
    RegexMatchValidValidatorDirective,
    MtxDatetimepickerModule,
  ],
  templateUrl: './seasonMatch.component.html',
  styleUrl: './seasonMatch.component.scss'
})
export class SeasonMatchComponent implements OnInit {
  private seasonMatchID: number = 0;
  seasonMatch: SeasonMatchPageDto | null = null;

  @ViewChild('reportMatchForm') reportMatchForm!: NgForm;

  matchID: string = "";
  regex: string = String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`;
  regexErrorMessage: string = "Must be a valid GUID";

  constructor(private route: ActivatedRoute, private http: HttpClient) {
  }

  ngOnInit(): void {
    this.seasonMatchID = Number(this.route.snapshot.paramMap.get('seasonMatchID'));

    this.getPageDto();
  }

  getPageDto(): void {
    this.http.get<SeasonMatchPageDto>('/api/SeasonMatch/GetSeasonMatchPage/' + this.seasonMatchID)
      .subscribe({
        next: result => this.seasonMatch = result,
      });
  }

  onSubmit(): void {
    this.http.get<string>('/api/SeasonMatch/ReportMatch/' + this.seasonMatchID + '/' + this.matchID)
      .subscribe({
        next: result => console.log(result),
      });
  }
}
