import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { ActivatedRoute } from '@angular/router';

@Component({
    selector: 'app-infinite-client',
    imports: [
        CommonModule,
        MatSnackBarModule,
        MatInputModule,
        MatFormFieldModule,
        FormsModule,
        MatButtonModule,
        ErrorMessageComponent,
    ],
    templateUrl: './infiniteClient.component.html',
    styleUrl: './infiniteClient.component.scss'
})
export class InfiniteClientComponent implements OnInit {
  code: string = "";
  status: string = "";

  constructor(private http: HttpClient, private snackbar: MatSnackBar, private route: ActivatedRoute) {
  }

  ngOnInit(): void {
    this.checkStatus();
    
    const c = String(this.route.snapshot.queryParamMap.get('code'));
    if (c !== "null")
      this.code = c;
  }

  setCode() {
    this.http.get("api/Admin/SetCode?code=" + this.code).subscribe({
      next: r => this.snackbar.open('Success', "Close"),
      error: r => {
        console.log(r);
        this.snackbar.open('Failed to set code', "Close");
      }
    })
  }

  checkStatus() {
    this.http.get<string>("api/Admin/CheckStatus", this.text).subscribe({
      next: r => this.status = r,
      error: r => {
        console.log(r);
        this.status = "Failed to check status";
        this.snackbar.open('Failed to check status', "Close");
      }
    })
  }

  deleteTokens() {
    this.http.get("api/Admin/DeleteTokens").subscribe({
      next: r => this.snackbar.open('Success', "Close"),
      error: r => {
        console.log(r);
        this.snackbar.open('Failed to delete tokens', "Close");
      }
    })
  }

  private readonly text : Object = {
    responseType: 'text'
  };
}
