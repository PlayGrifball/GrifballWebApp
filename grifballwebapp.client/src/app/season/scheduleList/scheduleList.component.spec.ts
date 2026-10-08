import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ScheduleListComponent } from './scheduleList.component';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { provideAnimations } from '@angular/platform-browser/animations';
import { JWT_OPTIONS, JwtHelperService } from '@auth0/angular-jwt';

describe('ScheduleListComponent', () => {
  let component: ScheduleListComponent;
  let fixture: ComponentFixture<ScheduleListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScheduleListComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: JWT_OPTIONS, useValue: {} },
        JwtHelperService,
        {
          provide: ActivatedRoute,
          useValue: {
            parent: {
              params: of({ id: 'test-season-id' })
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ScheduleListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
