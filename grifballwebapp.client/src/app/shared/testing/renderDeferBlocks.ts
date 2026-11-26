import { ComponentFixture, DeferBlockState } from '@angular/core/testing';

/**
 * Renders every `@defer` block of the fixture to the given state (`Complete` by default, as the browser draws it
 * once its code is in), then runs change detection. Pass `DeferBlockState.Error` to test the `@error` block, as if
 * the chunk failed to load.
 */
export async function renderDeferBlocks(fixture: ComponentFixture<unknown>, state: DeferBlockState = DeferBlockState.Complete): Promise<void> {
  for (const block of await fixture.getDeferBlocks())
    await block.render(state);
  fixture.detectChanges();
}
