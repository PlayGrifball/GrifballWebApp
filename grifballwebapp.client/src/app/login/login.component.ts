import { Component, input, OnInit, ChangeDetectionStrategy, PLATFORM_ID, computed, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { LoginDto } from '../api/dtos/loginDto';
import { RouterLink } from '@angular/router';
import { ErrorMessageComponent } from '../validation/errorMessage.component';
import { AccountService, appRelativePath } from '../account.service';
import { MatCardModule } from '@angular/material/card';

@Component({
    selector: 'app-login',
    imports: [
        MatInputModule,
        MatFormFieldModule,
        FormsModule,
        MatIconModule,
        MatButtonModule,
        RouterLink,
        ErrorMessageComponent,
        MatCardModule
    ],
    templateUrl: './login.component.html',
    styleUrl: './login.component.css',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent implements OnInit {
  accountService = inject(AccountService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  hide = true;

  model: LoginDto = { } as LoginDto;

  callback = input<string>();
  followUp = input<string>();

  /** Discord's sign-in, through the API, coming back to the follow-up if it is a path on this site. */
  readonly discordUrl = computed(() => {
    const followUp = appRelativePath(this.followUp());
    return followUp === null ? '/api/identity/externallogin' : '/api/identity/externallogin?followUp=' + encodeURIComponent(followUp);
  });

  ngOnInit(): void {
    // Signing in sets the browser's cookie, and leaving for Discord needs the window: the server only draws the form.
    if (!this.isBrowser)
      return;
    if (this.callback() === 'true') {
      // We are returning from a login, validate that it worked
      console.log('Returned from backend ExternalLoginCallback');
      this.accountService.loginExternal(this.followUp());
    } else { // Just loading the page... still check for a follow-up
      if (appRelativePath(this.followUp()) !== null) {
        // Once the API has said whether this browser is signed in
        this.accountService.whenKnown().subscribe(() => {
          if (this.accountService.isLoggedIn()) {
            // Go right to the follow-up
            console.log('Already logged in, redirecting to follow-up');
            this.accountService.goToFollowUp(this.followUp());
          } else {
            console.log('Not logged in, must log in before going to follow-up');
            this.leaveFor(this.discordUrl());
          }
        });
      }
    }
  }

  /** Leaves the app for `url` (Discord's sign-in, through the API). */
  protected leaveFor(url: string): void {
    window.location.href = url;
  }

  onSubmit() {
    this.accountService.login(this.model, this.followUp());
  }
}
