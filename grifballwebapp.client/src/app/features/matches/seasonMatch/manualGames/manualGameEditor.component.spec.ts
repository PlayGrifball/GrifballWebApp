import { ComponentFixture, TestBed } from '@angular/core/testing';
import { emptyForm, ManualGameForm } from './manualGameForm';
import { ManualGameEditorComponent } from './manualGameEditor.component';

describe('ManualGameEditorComponent', () => {
  let fixture: ComponentFixture<ManualGameEditorComponent>;
  let form: ManualGameForm;
  let saved: ManualGameForm[];
  let cancelled: number;

  beforeEach(() => {
    fixture = TestBed.createComponent(ManualGameEditorComponent);
    form = { ...emptyForm(), players: [{ userID: 3, gamertag: 'Tucker', isHomeTeam: false, played: false, kills: 0, deaths: 0, assists: 0, goals: 0, score: null }] };
    fixture.componentRef.setInput('form', form);
    fixture.componentRef.setInput('editing', 41);
    saved = [];
    cancelled = 0;
    fixture.componentInstance.saved.subscribe(value => saved.push(value));
    fixture.componentInstance.cancelled.subscribe(() => cancelled++);
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const button = (label: string) => Array.from(el().querySelectorAll('button')).find(b => b.textContent!.trim() === label)!;
  const field = (name: string) => el().querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
  /** Types into a field the way a commissioner would (its own `input` event, reactive forms' way of hearing it). */
  async function type(name: string, value: string): Promise<void> {
    field(name).value = value;
    field(name).dispatchEvent(new Event('input'));
    await fixture.whenStable();
    fixture.detectChanges();
  }
  const submit = () => el().querySelector('form')!.dispatchEvent(new Event('submit'));

  it('saves only a valid game: both scores in, and different', async () => {
    submit();
    expect(saved.length).toBe(0);
    expect(button('Save game').disabled).toBeTrue();

    await type('homeTeamScore', '3');
    await type('awayTeamScore', '3');
    expect(el().textContent).toContain("the two scores can't be equal");
    submit();
    expect(saved.length).toBe(0);

    await type('awayTeamScore', '5');
    expect(button('Save game').disabled).toBeFalse();
    submit();
    expect(saved.length).toBe(1);
    expect(saved[0]).toEqual(jasmine.objectContaining({ homeTeamScore: 3, awayTeamScore: 5 }));
  });

  it("emits the game with the fields edited, a player line at a time, the caller's form left alone", async () => {
    await fixture.whenStable();
    await type('map', 'Pit');
    expect(el().querySelector<HTMLInputElement>('input[aria-label="Tucker kills"]')!.disabled).toBeTrue();
    // Nothing reaches the caller's object until it is saved
    expect(form.map).toBe('');

    await type('homeTeamScore', '3');
    await type('awayTeamScore', '5');
    submit();
    expect(saved[0].map).toBe('Pit');
    expect(saved[0].players).toEqual([{ userID: 3, gamertag: 'Tucker', isHomeTeam: false, played: false, kills: 0, deaths: 0, assists: 0, goals: 0, score: null }]);
    expect(form.map).toBe('');
  });

  it('says it is cancelled, and sends nothing while a save or the page is busy', async () => {
    button('Cancel').click();
    expect(cancelled).toBe(1);

    await type('homeTeamScore', '3');
    await type('awayTeamScore', '5');
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
