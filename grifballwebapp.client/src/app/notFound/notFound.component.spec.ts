import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotFoundComponent } from './notFound.component';

describe('NotFoundComponent', () => {
  let component: NotFoundComponent;
  let fixture: ComponentFixture<NotFoundComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotFoundComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(NotFoundComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('says the page was not found and links home and to the leaderboards', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')!.textContent).toBe('This page got hammered');
    const links = [...el.querySelectorAll('a')];
    expect(links.map(a => a.getAttribute('href'))).toEqual(['/', '/leaderboards']);
    expect(links.map(a => a.textContent!.trim())).toEqual(['Respawn at home', "See who's on top"]);
  });
});
