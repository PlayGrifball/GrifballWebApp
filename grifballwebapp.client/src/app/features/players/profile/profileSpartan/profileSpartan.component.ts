import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnInit, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormRecord, ReactiveFormsModule } from '@angular/forms';
import type { SpartanCatalogCore, SpartanCatalogWeapon, SpartanChoice } from '@api/models';
import { SpartanShowcaseComponent } from '../spartanShowcase/spartanShowcase.component';
import { ProfileSpartanStore } from './profileSpartan.store';

/** Grifball's own weapons: every rendered model also holds them, in the poses that carry them. */
const GRIFBALL_WEAPONS = ['Gravity Hammer', 'Energy Sword'];

/** The signed-in user's Spartan on their profile: the files rendered for them, and a choice of core, pieces, coating, visor and weapon coatings that replaces their equipped set. */
@Component({
  selector: 'app-profile-spartan',
  imports: [DatePipe, ReactiveFormsModule, SpartanShowcaseComponent],
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
    visor: new FormControl('', { nonNullable: true }),
    disableDamage: new FormControl(true, { nonNullable: true }),
    weaponCoatings: new FormRecord<FormControl<string>>({}),
  });
  /** The pieces the open form starts from, for the controls a region's arrival creates. */
  private seed: Record<string, string> = {};

  readonly core = toSignal(this.form.controls.core.valueChanges, { initialValue: '' });
  readonly cores = computed(() => this.catalog()?.cores ?? []);
  readonly selectedCore = computed<SpartanCatalogCore | undefined>(() => this.cores().find(c => c.name === this.core()));
  /** The weapons whose coating the choice can pick: the one the rendered stance holds, then the Grifball weapons. */
  readonly coatableWeapons = computed(() => {
    const weapons = this.catalog()?.weapons ?? [];
    const names = [...new Set([this.profile()?.weapon, ...GRIFBALL_WEAPONS].filter((n): n is string => !!n))];
    return names.map(n => weapons.find(w => w.name === n)).filter((w): w is SpartanCatalogWeapon => !!w);
  });
  /** The core's regions that name their pieces, which the choice can pick by name. */
  readonly regions = computed(() =>
    (this.selectedCore()?.regions ?? []).filter(r => r.kind === 'REGION' && r.pieces.some(p => p.name)));

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
    effect(() => {
      this.coatableWeapons();
      untracked(() => this.syncWeaponCoatings());
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  edit(): void {
    const choice = this.profile()?.choice;
    this.form.controls.core.setValue(choice?.core ?? '');
    this.form.controls.coating.setValue(choice?.coating ?? '');
    this.form.controls.visor.setValue(choice?.visor ?? '');
    this.form.controls.disableDamage.setValue(choice?.disableDamage ?? true);
    for (const name of Object.keys(this.form.controls.weaponCoatings.controls))
      this.form.controls.weaponCoatings.removeControl(name);
    this.syncWeaponCoatings();
    this.seed = { ...(choice?.armor ?? {}) };
    this.syncPieces();
    this.store.loadCatalog();
    this.editing.set(true);
  }

  cancel(): void {
    this.editing.set(false);
  }

  save(): void {
    const { core, coating, visor, disableDamage, armor, weaponCoatings } = this.form.getRawValue();
    const choice: SpartanChoice = {
      core,
      armor: Object.fromEntries(Object.entries(armor).filter(([, piece]) => piece)),
      parts: this.profile()?.choice?.core === core ? this.profile()?.choice?.parts ?? null : null,
      coating: coating || null,
      visor: visor || null,
      disableDamage,
      weaponCoatings: this.weaponCoatings(weaponCoatings),
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

  /** The coatings chosen per weapon: each offered weapon gets the picked one (or none for Equipped in Halo), the others keep theirs. */
  private weaponCoatings(picked: Record<string, string>): Record<string, string> | null {
    const coatings: Record<string, string> = { ...(this.profile()?.choice?.weaponCoatings ?? {}) };
    for (const [weapon, coating] of Object.entries(picked)) {
      if (coating)
        coatings[weapon] = coating;
      else
        delete coatings[weapon];
    }
    return Object.keys(coatings).length ? coatings : null;
  }

  /** An id for a weapon's select. */
  coatingId(weapon: SpartanCatalogWeapon): string {
    return 'spartan-coating-' + weapon.name.replace(/[^A-Za-z0-9]+/g, '-');
  }

  private syncWeaponCoatings(): void {
    const coatings = this.form.controls.weaponCoatings;
    const chosen = this.profile()?.choice?.weaponCoatings ?? {};
    for (const weapon of this.coatableWeapons().filter(w => !coatings.contains(w.name)))
      coatings.addControl(weapon.name, new FormControl(chosen[weapon.name] ?? '', { nonNullable: true }));
  }

  private syncPieces(): void {
    const armor = this.form.controls.armor;
    for (const key of this.regions().map(r => r.key).filter(k => !armor.contains(k)))
      armor.addControl(key, new FormControl(this.seed[key] ?? '', { nonNullable: true }));
  }
}
