import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeasonMatchComponent } from './seasonMatch.component';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { JWT_OPTIONS, JwtHelperService } from '@auth0/angular-jwt';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

describe('SeasonMatchComponent', () => {
  let component: SeasonMatchComponent;
  let fixture: ComponentFixture<SeasonMatchComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonMatchComponent],
      providers: [
        provideHttpClient(),
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

    fixture = TestBed.createComponent(SeasonMatchComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
