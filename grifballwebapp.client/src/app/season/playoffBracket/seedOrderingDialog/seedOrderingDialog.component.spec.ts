import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SeedOrderingDialogComponent } from './seedOrderingDialog.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('SeedOrderingDialogComponent', () => {
  let component: SeedOrderingDialogComponent;
  let fixture: ComponentFixture<SeedOrderingDialogComponent>;

  beforeEach(async () => {
    const mockDialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [SeedOrderingDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimations(),
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { seasonID: 'test-season-id' } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SeedOrderingDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
