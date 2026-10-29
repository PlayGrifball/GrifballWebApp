import { DndDropzoneDirective } from 'ngx-drag-drop';
import { placeholderNullWhenNone } from './dndServer';

describe('placeholderNullWhenNone', () => {
  // A dropzone's prototype as on the server: querySelector (domino) answers undefined for none
  const dropzone = (placeholder: Element | undefined) => ({ tryGetPlaceholder: () => placeholder as Element | null });

  it('answers null for a dropzone without a placeholder', () => {
    const proto = dropzone(undefined);
    placeholderNullWhenNone(proto);
    expect(proto.tryGetPlaceholder()).toBeNull();
  });

  it('keeps a placeholder that is there', () => {
    const placeholder = document.createElement('div');
    const proto = dropzone(placeholder);
    placeholderNullWhenNone(proto);
    expect(proto.tryGetPlaceholder()).toBe(placeholder);
  });

  it('wraps once, however many times it runs', () => {
    const proto = dropzone(undefined);
    placeholderNullWhenNone(proto);
    const wrapped = proto.tryGetPlaceholder;
    placeholderNullWhenNone(proto);
    expect(proto.tryGetPlaceholder).toBe(wrapped);
  });

  it('finds the method it wraps on ngx-drag-drop\'s dropzone (a private one, which an update may rename)', () => {
    expect(typeof (DndDropzoneDirective.prototype as unknown as Record<string, unknown>)['tryGetPlaceholder']).toBe('function');
  });
});
