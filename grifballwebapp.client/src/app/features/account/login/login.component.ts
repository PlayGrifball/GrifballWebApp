import { Component, input, OnInit, ChangeDetectionStrategy, PLATFORM_ID, computed, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import type { LoginDto } from '@api/models';
import { RouterLink } from '@angular/router';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { AccountService, appRelativePath } from '@core/auth/account.service';
import { MatCardModule } from '@angular/material/card';
import { AppLog } from '@core/telemetry/appLog';

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
  private readonly log = inject(AppLog);
  accountService = inject(AccountService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  hide = true;

  model: LoginDto = { } as LoginDto;

  callback = input<string>();
  followUp = input<string>();

  /** Discord's sign-in, through the API, coming back to the follow-up if it is a path on this site. */
  readonly discordUrl = computed(() => {
    const followUp = appRelativePath(this.followUp());
    return followUp === null ? '/api/identity/external-login' : '/api/identity/external-login?followUp=' + encodeURIComponent(followUp);
  });

  ngOnInit(): void {
    // Signing in sets the browser's cookie, and leaving for Discord needs the window: the server only draws the form.
    if (!this.isBrowser)
      return;
    if (this.callback() === 'true') {
      // We are returning from a login, validate that it worked
      this.log.info('Sign-in: back from the external login');
      this.accountService.loginExternal(this.followUp());
    } else { // Just loading the page... still check for a follow-up
      if (appRelativePath(this.followUp()) !== null) {
        // Once the API has said whether this browser is signed in
        this.accountService.whenKnown().subscribe(() => {
          if (this.accountService.isLoggedIn()) {
            // Go right to the follow-up
            this.log.info('Sign-in: already signed in, going to the follow-up');
            this.accountService.goToFollowUp(this.followUp());
          } else {
            this.log.info('Sign-in: not signed in, signing in before the follow-up');
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
