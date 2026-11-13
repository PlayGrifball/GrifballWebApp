import { Provider } from '@angular/core';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';

/**
 * For TestBed: Angular Material's animations off, as NoopAnimationsModule turned them off before the app left
 * @angular/animations (deprecated). Dialogs, menus and the like open and close at once.
 */
export const noMaterialAnimations: Provider = { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } };
