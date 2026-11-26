import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import type { SpartanCatalogCore, SpartanChoice } from '@api/models';
import { ProfileSpartanStore } from './profileSpartan.store';

/** The signed-in user's Spartan on their profile: the files rendered for them, and a choice of core, pieces, coating and visor that replaces their equipped set. */
@Component({
  selector: 'app-profile-spartan',
  imports: [DatePipe],
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
  readonly core = signal('');
  readonly armor = signal<Record<string, string>>({});
  readonly coating = signal('');
  readonly visor = signal('');

  readonly cores = computed(() => this.catalog()?.cores ?? []);
  readonly selectedCore = computed<SpartanCatalogCore | undefined>(() => this.cores().find(c => c.name === this.core()));
  /** The core's regions that name their pieces, which the choice can pick by name. */
  readonly regions = computed(() =>
    (this.selectedCore()?.regions ?? []).filter(r => r.kind === 'REGION' && r.pieces.some(p => p.name)));

  readonly fileUrl = (name: string): string => `/api/users/${this.userID()}/spartan/${name}`;
  readonly hasFile = (name: string): boolean => !!this.profile()?.files?.includes(name);

  ngOnInit(): void {
    this.store.load();
  }

  edit(): void {
    const choice = this.profile()?.choice;
    this.core.set(choice?.core ?? '');
    this.armor.set({ ...(choice?.armor ?? {}) });
    this.coating.set(choice?.coating ?? '');
    this.visor.set(choice?.visor ?? '');
    this.store.loadCatalog();
    this.editing.set(true);
  }

  cancel(): void {
    this.editing.set(false);
  }

  setCore(name: string): void {
    this.core.set(name);
    this.armor.set({});
  }

  setPiece(key: string, name: string): void {
    this.armor.update(armor => {
      const next = { ...armor };
      if (name) next[key] = name;
      else delete next[key];
      return next;
    });
  }

  save(): void {
    const choice: SpartanChoice = {
      core: this.core(),
      armor: this.armor(),
      parts: this.profile()?.choice?.core === this.core() ? this.profile()?.choice?.parts ?? null : null,
      coating: this.coating() || null,
      visor: this.visor() || null,
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
}
