import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Notifier } from './notifier.service';
import { SNACK_BAR_DURATION_MS } from './snackBarDefaults';

describe('Notifier', () => {
  it('opens a snack bar that closes itself, once the snack bar code has loaded', async () => {
    const snackBar = jasmine.createSpyObj<MatSnackBar>('MatSnackBar', ['open']);
    TestBed.configureTestingModule({ providers: [{ provide: MatSnackBar, useValue: snackBar }] });

    await TestBed.inject(Notifier).open('Login failed', 'Close');

    expect(snackBar.open).toHaveBeenCalledOnceWith('Login failed', 'Close', { duration: SNACK_BAR_DURATION_MS });
  });
});
