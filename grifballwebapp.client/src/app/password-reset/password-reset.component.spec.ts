import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { PasswordResetComponent } from './password-reset.component';
import { AccountService } from '../account.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

describe('PasswordResetComponent', () => {
  let component: PasswordResetComponent;
  let fixture: ComponentFixture<PasswordResetComponent>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;
  let mockActivatedRoute: jasmine.SpyObj<ActivatedRoute>;

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['resetPassword']);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    mockActivatedRoute = jasmine.createSpyObj('ActivatedRoute', [], { snapshot: {} });

    await TestBed.configureTestingModule({
      imports: [PasswordResetComponent, NoopAnimationsModule],
      providers: [
        { provide: AccountService, useValue: mockAccountService },
        { provide: Router, useValue: mockRouter },
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PasswordResetComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with hide set to true', () => {
    expect(component.hide).toBe(true);
  });

  it('should initialize with hideConfirm set to true', () => {
    expect(component.hideConfirm).toBe(true);
  });

  it('should initialize with isLoading set to false', () => {
    expect(component.isLoading).toBe(false);
  });

  it('should initialize with empty model', () => {
    expect(component.model.newPassword).toBe('');
    expect(component.model.confirmPassword).toBe('');
  });

  describe('ngOnInit', () => {
    it('should show error and navigate to login when token is not provided', () => {
      fixture.componentRef.setInput('token', undefined);
      
      component.ngOnInit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Invalid password reset link', 'Close');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should show error and navigate to login when token is empty', () => {
      fixture.componentRef.setInput('token', '');
      
      component.ngOnInit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Invalid password reset link', 'Close');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should not show error when token is provided', () => {
      fixture.componentRef.setInput('token', 'validtoken123');
      
      component.ngOnInit();

      expect(mockSnackBar.open).not.toHaveBeenCalled();
      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });
  });

  describe('onSubmit', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('token', 'validtoken123');
    });

    it('should show error when passwords do not match', () => {
      component.model.newPassword = 'password1';
      component.model.confirmPassword = 'password2';
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Passwords do not match', 'Close');
      expect(mockAccountService.resetPassword).not.toHaveBeenCalled();
    });

    it('should show error when password is too short', () => {
      component.model.newPassword = '12345';
      component.model.confirmPassword = '12345';
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Password must be at least 6 characters long', 'Close');
      expect(mockAccountService.resetPassword).not.toHaveBeenCalled();
    });

    it('should set isLoading to true when submitting valid password', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(of('Success'));
      
      component.onSubmit();

      expect(component.isLoading).toBe(true);
    });

    it('should call resetPassword with correct request when passwords match and are valid', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(of('Success'));
      
      component.onSubmit();

      expect(mockAccountService.resetPassword).toHaveBeenCalledWith({
        token: 'validtoken123',
        newPassword: 'ValidPassword123'
      });
    });

    it('should show success message and navigate to login on successful reset', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(of('Success'));
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith(
        'Password reset successfully! You can now log in with your new password.', 
        'Close',
        { duration: 5000 }
      );
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should show generic error message on error without specific message', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(throwError(() => ({ status: 500 })));
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Failed to reset password', 'Close');
      expect(component.isLoading).toBe(false);
    });

    it('should show specific error message when provided as string', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(
        throwError(() => ({ error: 'Token has expired', status: 400 }))
      );
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Token has expired', 'Close');
      expect(component.isLoading).toBe(false);
    });

    it('should show default 400 error message when status is 400 without specific error', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(
        throwError(() => ({ error: {}, status: 400 }))
      );
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Invalid or expired reset link', 'Close');
      expect(component.isLoading).toBe(false);
    });

    it('should not navigate to login on error', () => {
      component.model.newPassword = 'ValidPassword123';
      component.model.confirmPassword = 'ValidPassword123';
      mockAccountService.resetPassword.and.returnValue(throwError(() => ({ status: 500 })));
      
      component.onSubmit();

      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });

    it('should work with minimum valid password length', () => {
      component.model.newPassword = '123456';
      component.model.confirmPassword = '123456';
      mockAccountService.resetPassword.and.returnValue(of('Success'));
      
      component.onSubmit();

      expect(mockAccountService.resetPassword).toHaveBeenCalled();
      expect(mockSnackBar.open).toHaveBeenCalledWith(
        'Password reset successfully! You can now log in with your new password.', 
        'Close',
        { duration: 5000 }
      );
    });

    it('should handle empty passwords', () => {
      component.model.newPassword = '';
      component.model.confirmPassword = '';
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Password must be at least 6 characters long', 'Close');
      expect(mockAccountService.resetPassword).not.toHaveBeenCalled();
    });
  });
});
