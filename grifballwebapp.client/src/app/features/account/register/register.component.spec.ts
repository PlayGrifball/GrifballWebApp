import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RegisterComponent } from './register.component';
import { AccountService } from '@core/auth/account.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { noMaterialAnimations } from '@shared/testing/noMaterialAnimations';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;
  let mockActivatedRoute: jasmine.SpyObj<ActivatedRoute>;

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['register']);
    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    mockActivatedRoute = jasmine.createSpyObj('ActivatedRoute', [], { snapshot: {} });

    await TestBed.configureTestingModule({
      imports: [RegisterComponent, FormsModule],
      providers: [
        noMaterialAnimations,
        { provide: AccountService, useValue: mockAccountService },
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with hidePassword set to true', () => {
    expect(component.hidePassword).toBe(true);
  });

  it('should initialize with hideConfirmPassword set to true', () => {
    expect(component.hideConfirmPassword).toBe(true);
  });

  it('should have password regex pattern defined', () => {
    expect(component.regex).toBeDefined();
    expect(component.regex.length).toBeGreaterThan(0);
  });

  it('should have regex error message defined', () => {
    expect(component.regexErrorMessage).toContain('Minimum 12 characters');
  });

  it('should initialize with empty model', () => {
    expect(component.model).toBeDefined();
  });

  describe('onSubmit', () => {
    // registerForm is a viewChild() signal; swap it for one that returns the stand-in form.
    const stubForm = (form: unknown) => (component as any).registerForm = () => form;

    it('should return early if registerForm is not defined', () => {
      stubForm(undefined);
      
      component.onSubmit();

      expect(mockAccountService.register).not.toHaveBeenCalled();
    });

    it('should return early if form is invalid', () => {
      stubForm({
        valid: false
      });
      
      component.onSubmit();

      expect(mockAccountService.register).not.toHaveBeenCalled();
    });

    it('should call register when form is valid', () => {
      component.model = {
        username: 'testuser',
        password: 'Test@1234567890',
        confirmPassword: 'Test@1234567890',
        gamertag: 'TestGT'
      };
      stubForm({
        valid: true
      });
      mockAccountService.register.and.returnValue(of(undefined));
      
      component.onSubmit();

      expect(mockAccountService.register).toHaveBeenCalledWith(component.model);
    });

    it('should show success message on successful registration', () => {
      stubForm({
        valid: true
      });
      mockAccountService.register.and.returnValue(of(undefined));
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Registration Success', 'Close');
    });

    it('should show error message on registration failure', () => {
      stubForm({
        valid: true
      });
      const error = new HttpErrorResponse({ status: 400, error: { title: 'Request refused', status: 400, detail: 'DuplicateUserName: Username already exists' } });
      mockAccountService.register.and.returnValue(throwError(() => error));
      
      component.onSubmit();

      expect(mockSnackBar.open).toHaveBeenCalledWith('Registration failed: DuplicateUserName: Username already exists', 'Close');
    });

    it('should handle registration with full model', () => {
      component.model = {
        username: 'newuser',
        password: 'SecurePass123!@#',
        confirmPassword: 'SecurePass123!@#',
        gamertag: 'NewGamerTag'
      };
      stubForm({
        valid: true
      });
      mockAccountService.register.and.returnValue(of(undefined));
      
      component.onSubmit();

      expect(mockAccountService.register).toHaveBeenCalledWith(jasmine.objectContaining({
        username: 'newuser',
        password: 'SecurePass123!@#',
        gamertag: 'NewGamerTag'
      }));
      expect(mockSnackBar.open).toHaveBeenCalledWith('Registration Success', 'Close');
    });

    it('should use hidePassword property to toggle password visibility', () => {
      expect(component.hidePassword).toBe(true);
      
      component.hidePassword = false;
      expect(component.hidePassword).toBe(false);
      
      component.hidePassword = true;
      expect(component.hidePassword).toBe(true);
    });

    it('should use hideConfirmPassword property to toggle confirm password visibility', () => {
      expect(component.hideConfirmPassword).toBe(true);
      
      component.hideConfirmPassword = false;
      expect(component.hideConfirmPassword).toBe(false);
      
      component.hideConfirmPassword = true;
      expect(component.hideConfirmPassword).toBe(true);
    });
  });
});
