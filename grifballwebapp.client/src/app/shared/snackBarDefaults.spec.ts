import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS, MatSnackBarConfig } from '@angular/material/snack-bar';
import { provideSnackBarDefaults, SNACK_BAR_DURATION_MS } from './snackBarDefaults';

describe('provideSnackBarDefaults', () => {
  it('sets the default snack bar duration once the app is stable', async () => {
    TestBed.configureTestingModule({ providers: [provideSnackBarDefaults()] });
    const defaults: MatSnackBarConfig = TestBed.inject(MAT_SNACK_BAR_DEFAULT_OPTIONS);
    expect(defaults.duration).toBe(0);

    await TestBed.inject(ApplicationRef).whenStable();
    // Then the snack bar code is imported (already loaded in the tests, but still a few ticks).
    for (let i = 0; i < 100 && defaults.duration === 0; i++)
      await new Promise(resolve => setTimeout(resolve, 10));

    expect(defaults.duration).toBe(SNACK_BAR_DURATION_MS);
  });

  it('leaves the options alone when the app is gone before it is stable', async () => {
    TestBed.configureTestingModule({ providers: [provideSnackBarDefaults()] });
    const defaults: MatSnackBarConfig = TestBed.inject(MAT_SNACK_BAR_DEFAULT_OPTIONS);
    TestBed.resetTestingModule();

    await new Promise(resolve => setTimeout(resolve));

    expect(defaults.duration).toBe(0);
  });
});
