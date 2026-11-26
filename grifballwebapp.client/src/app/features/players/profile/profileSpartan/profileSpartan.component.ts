import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnInit, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormRecord, ReactiveFormsModule } from '@angular/forms';
import type { SpartanCatalogCore, SpartanChoice } from '@api/models';
import { ProfileSpartanStore } from './profileSpartan.store';

/** The signed-in user's Spartan on their profile: the files rendered for them, and a choice of core, pieces and coating that replaces their equipped set. */
@Component({
  selector: 'app-profile-spartan',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './profileSpartan.component.html',
  styleUrl: './profileSpartan.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ProfileSpartanStore],
})
export class ProfileSpartanComponent implements OnInit {
  private readonly store = inject(ProfileSpartanStore);

  readonly userID = input.required<number>();

  readonly profile = this.store.profile;
  readonly catalog = this.store.catalog;
  readonly busy = this.store.busy;

  readonly editing = signal(false);
  readonly form = new FormGroup({
    core: new FormControl('', { nonNullable: true }),
    armor: new FormRecord<FormControl<string>>({}),
    coating: new FormControl('', { nonNullable: true }),
  });
  /** The pieces the open form starts from, for the controls a region's arrival creates. */
  private seed: Record<string, string> = {};

  readonly core = toSignal(this.form.controls.core.valueChanges, { initialValue: '' });
  readonly cores = computed(() => this.catalog()?.cores ?? []);
  readonly selectedCore = computed<SpartanCatalogCore | undefined>(() => this.cores().find(c => c.name === this.core()));
  /** The core's regions that name their pieces, which the choice can pick by name. */
  readonly regions = computed(() =>
    (this.selectedCore()?.regions ?? []).filter(r => r.kind === 'REGION' && r.pieces.some(p => p.name)));

  /** The render's time is in the address, so a new render is not shown from the cache of an old one. */
  readonly fileUrl = (name: string): string => {
    const rendered = this.profile()?.renderedAt;
    const version = rendered ? `?v=${encodeURIComponent(rendered)}` : '';
    return `/api/users/${this.userID()}/spartan/${name}${version}`;
  };
  readonly hasFile = (name: string): boolean => !!this.profile()?.files?.includes(name);

  constructor() {
    this.form.controls.core.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.seed = {};
      for (const key of Object.keys(this.form.controls.armor.controls))
        this.form.controls.armor.removeControl(key);
    });
    effect(() => {
      this.regions();
      untracked(() => this.syncPieces());
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  edit(): void {
    const choice = this.profile()?.choice;
    this.form.controls.core.setValue(choice?.core ?? '');
    this.form.controls.coating.setValue(choice?.coating ?? '');
    this.seed = { ...(choice?.armor ?? {}) };
    this.syncPieces();
    this.store.loadCatalog();
    this.editing.set(true);
  }

  cancel(): void {
    this.editing.set(false);
  }

  save(): void {
    const { core, coating, armor } = this.form.getRawValue();
    const choice: SpartanChoice = {
      core,
      armor: Object.fromEntries(Object.entries(armor).filter(([, piece]) => piece)),
      parts: this.profile()?.choice?.core === core ? this.profile()?.choice?.parts ?? null : null,
      coating: coating || null,
    };
    this.store.save(choice);
    this.editing.set(false);
  }

  reset(): void {
    this.store.reset();
    this.editing.set(false);
  }

  render(): void {
    this.store.render();
  }

  private syncPieces(): void {
    const armor = this.form.controls.armor;
    for (const key of this.regions().map(r => r.key).filter(k => !armor.contains(k)))
      armor.addControl(key, new FormControl(this.seed[key] ?? '', { nonNullable: true }));
  }
}
