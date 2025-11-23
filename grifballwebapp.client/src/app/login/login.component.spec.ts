import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { LoginComponent } from './login.component';
import { AccountService } from '../account.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ComponentRef } from '@angular/core';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockActivatedRoute: jasmine.SpyObj<ActivatedRoute>;

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['login', 'loginExternal', 'isLoggedIn']);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockActivatedRoute = jasmine.createSpyObj('ActivatedRoute', [], { snapshot: {} });

    await TestBed.configureTestingModule({
      imports: [LoginComponent, NoopAnimationsModule],
      providers: [
        { provide: AccountService, useValue: mockAccountService },
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with hide set to true', () => {
    expect(component.hide).toBe(true);
  });

  it('should initialize with empty model', () => {
    expect(component.model).toBeDefined();
  });

  it('should have accountService injected', () => {
    expect(component.accountService).toBe(mockAccountService);
  });

  describe('ngOnInit', () => {
    it('should call loginExternal when callback is true', () => {
      fixture.componentRef.setInput('callback', 'true');
      fixture.componentRef.setInput('followUp', '/dashboard');
      
      component.ngOnInit();

      expect(mockAccountService.loginExternal).toHaveBeenCalledWith('/dashboard');
    });

    it('should call loginExternal with empty string when no followUp provided', () => {
      fixture.componentRef.setInput('callback', 'true');
      fixture.componentRef.setInput('followUp', undefined);
      
      component.ngOnInit();

      expect(mockAccountService.loginExternal).toHaveBeenCalledWith('');
    });

    it('should navigate to followUp when already logged in', () => {
      fixture.componentRef.setInput('callback', undefined);
      fixture.componentRef.setInput('followUp', '/dashboard');
      mockAccountService.isLoggedIn.and.returnValue(true);
      
      component.ngOnInit();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/dashboard']);
    });

    it('should do nothing when callback is not true and no followUp', () => {
      fixture.componentRef.setInput('callback', undefined);
      fixture.componentRef.setInput('followUp', '');
      
      component.ngOnInit();

      expect(mockAccountService.loginExternal).not.toHaveBeenCalled();
      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });

    it('should do nothing when callback is false and followUp is undefined', () => {
      fixture.componentRef.setInput('callback', 'false');
      fixture.componentRef.setInput('followUp', undefined);
      
      component.ngOnInit();

      expect(mockAccountService.loginExternal).not.toHaveBeenCalled();
      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });
  });

  describe('onSubmit', () => {
    it('should call accountService.login with model', () => {
      component.model = { username: 'testuser', password: 'testpass' };
      
      component.onSubmit();

      expect(mockAccountService.login).toHaveBeenCalledWith({ username: 'testuser', password: 'testpass' });
    });

    it('should call accountService.login even with empty model', () => {
      component.model = { username: '', password: '' };
      
      component.onSubmit();

      expect(mockAccountService.login).toHaveBeenCalledWith(jasmine.objectContaining({ username: '', password: '' }));
    });
  });
});
