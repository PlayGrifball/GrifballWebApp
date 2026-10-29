import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { LoginComponent } from './login.component';
import { AccountService } from '../account.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ComponentRef, PLATFORM_ID } from '@angular/core';
import { of } from 'rxjs';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockActivatedRoute: jasmine.SpyObj<ActivatedRoute>;

  beforeEach(async () => {
    mockAccountService = jasmine.createSpyObj('AccountService', ['login', 'loginExternal', 'isLoggedIn', 'whenKnown']);
    mockAccountService.whenKnown.and.returnValue(of(null));
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

    it('sends a visitor to Discord\'s sign-in, with the follow-up', () => {
      fixture.componentRef.setInput('callback', undefined);
      fixture.componentRef.setInput('followUp', '/dashboard');
      mockAccountService.isLoggedIn.and.returnValue(false);
      const leaveFor = spyOn(component as unknown as { leaveFor: (url: string) => void }, 'leaveFor');

      component.ngOnInit();

      expect(leaveFor).toHaveBeenCalledOnceWith('/api/identity/externallogin?followUp=/dashboard');
      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });

    it('waits until the API has said who is signed in before going on', () => {
      fixture.componentRef.setInput('callback', undefined);
      fixture.componentRef.setInput('followUp', '/dashboard');
      mockAccountService.whenKnown.and.returnValue(of());
      mockAccountService.isLoggedIn.and.returnValue(true);

      component.ngOnInit();

      expect(mockRouter.navigate).not.toHaveBeenCalled();
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

  describe('on the server', () => {
    it('only draws the form: no sign-in and no trip to Discord', () => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        imports: [LoginComponent, NoopAnimationsModule],
        providers: [
          { provide: AccountService, useValue: mockAccountService },
          { provide: Router, useValue: mockRouter },
          { provide: ActivatedRoute, useValue: mockActivatedRoute },
          { provide: PLATFORM_ID, useValue: 'server' },
        ],
      });
      const serverFixture = TestBed.createComponent(LoginComponent);
      serverFixture.componentRef.setInput('callback', 'true');
      serverFixture.componentRef.setInput('followUp', '/dashboard');

      serverFixture.componentInstance.ngOnInit();

      expect(mockAccountService.loginExternal).not.toHaveBeenCalled();
      expect(mockAccountService.whenKnown).not.toHaveBeenCalled();
    });
  });
});
