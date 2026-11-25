import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReportMatchFormComponent } from './reportMatchForm.component';

describe('ReportMatchFormComponent', () => {
  let fixture: ComponentFixture<ReportMatchFormComponent>;
  let submitted: string[];

  beforeEach(async () => {
    fixture = TestBed.createComponent(ReportMatchFormComponent);
    submitted = [];
    fixture.componentInstance.submitted.subscribe(id => submitted.push(id));
    fixture.detectChanges();
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const input = () => el().querySelector<HTMLInputElement>('input[name="MatchID"]')!;
  const submit = () => el().querySelector('button')!;
  async function type(value: string): Promise<void> {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('only accepts a GUID as a match id, and says so', async () => {
    await type('nope');
    expect(submit().disabled).toBeTrue();
    expect(el().textContent).toContain('Must be a valid GUID');

    await type('0f8fad5b-d9cb-469f-a165-70867728950e');
    expect(submit().disabled).toBeFalse();
    expect(fixture.componentInstance.matchID()).toBe('0f8fad5b-d9cb-469f-a165-70867728950e');
  });

  it('reports the match id typed in, and nothing while the page is busy', async () => {
    await type('{0f8fad5b-d9cb-469f-a165-70867728950e}');
    submit().click();
    expect(submitted).toEqual(['{0f8fad5b-d9cb-469f-a165-70867728950e}']);

    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(submit().disabled).toBeTrue();
  });
});
