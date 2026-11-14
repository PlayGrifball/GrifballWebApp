import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateUserStore } from './createUser.store';
import { CreateUserDto } from './createUserDto';

describe('CreateUserStore', () => {
  let store: CreateUserStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [CreateUserStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(CreateUserStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('makes the user, and says how it went', () => {
    spyOn(console, 'log');
    const user = { userName: 'grif' } as unknown as CreateUserDto;
    store.create(user);
    const create = http.expectOne({ method: 'POST', url: '/api/users' });
    expect(create.request.body).toEqual(user);
    create.flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Created User');

    store.create(user);
    http.expectOne('/api/users').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to create user');
  });
});
