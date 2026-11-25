import { ChangeDetectorRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { emptyForm, ManualGameForm } from './manualGameForm';
import { ManualGameEditorComponent } from './manualGameEditor.component';

describe('ManualGameEditorComponent', () => {
  let fixture: ComponentFixture<ManualGameEditorComponent>;
  let form: ManualGameForm;
  let saved: number;
  let cancelled: number;

  beforeEach(() => {
    fixture = TestBed.createComponent(ManualGameEditorComponent);
    form = { ...emptyForm(), players: [{ userID: 3, gamertag: 'Tucker', isHomeTeam: false, played: false, kills: 0, deaths: 0, assists: 0, goals: 0, score: null }] };
    fixture.componentRef.setInput('form', form);
    fixture.componentRef.setInput('editing', 41);
    saved = 0;
    cancelled = 0;
    fixture.componentInstance.saved.subscribe(() => saved++);
    fixture.componentInstance.cancelled.subscribe(() => cancelled++);
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const button = (label: string) => Array.from(el().querySelectorAll('button')).find(b => b.textContent!.trim() === label)!;
  /** The form changed as the fields would change it (OnPush: a field's own input event marks it). */
  const refresh = () => {
    fixture.componentRef.injector.get(ChangeDetectorRef).markForCheck();
    fixture.detectChanges();
  };
  const submit = () => el().querySelector('form')!.dispatchEvent(new Event('submit'));

  it('saves only a valid game: both scores in, and different', () => {
    submit();
    expect(saved).toBe(0);
    expect(button('Save game').disabled).toBeTrue();

    form.homeTeamScore = 3;
    form.awayTeamScore = 3;
    refresh();
    expect(el().textContent).toContain("the two scores can't be equal");
    submit();
    expect(saved).toBe(0);

    form.awayTeamScore = 5;
    refresh();
    expect(button('Save game').disabled).toBeFalse();
    submit();
    expect(saved).toBe(1);
  });

  it('edits the caller\'s form in place, a player line at a time', async () => {
    // The fields register with the form a microtask after they are drawn
    await fixture.whenStable();
    const input = el().querySelector<HTMLInputElement>('input[name="map"]')!;
    input.value = 'Pit';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(form.map).toBe('Pit');
    expect(el().querySelector<HTMLInputElement>('input[aria-label="Tucker kills"]')!.disabled).toBeTrue();
  });

  it('says it is cancelled, and sends nothing while a save or the page is busy', () => {
    button('Cancel').click();
    expect(cancelled).toBe(1);

    form.homeTeamScore = 3;
    form.awayTeamScore = 5;
    fixture.componentRef.setInput('blocked', true);
    fixture.detectChanges();
    expect(button('Save game').disabled).toBeTrue();
    fixture.componentRef.setInput('blocked', false);
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();
    expect(button('Save game').disabled).toBeTrue();
    expect(button('Cancel').disabled).toBeTrue();
  });
});
