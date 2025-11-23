import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlayoffBracketComponent } from './playoffBracket.component';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { provideAnimations } from '@angular/platform-browser/animations';
import { JWT_OPTIONS, JwtHelperService } from '@auth0/angular-jwt';

describe('PlayoffBracketComponent', () => {
  let component: PlayoffBracketComponent;
  let fixture: ComponentFixture<PlayoffBracketComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlayoffBracketComponent],
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

    fixture = TestBed.createComponent(PlayoffBracketComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
