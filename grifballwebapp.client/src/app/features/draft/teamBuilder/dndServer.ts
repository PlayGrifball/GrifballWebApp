import { DndDropzoneDirective } from 'ngx-drag-drop';

type Dropzone = { tryGetPlaceholder(): Element | null, placeholderNullWhenNone?: true };

/**
 * ngx-drag-drop's dropzone looks for its placeholder (dndPlaceholderRef) with querySelector, and
 * takes null for none, as browsers answer. The server's DOM (domino) answers undefined: a dropzone
 * without a placeholder (a team's players, on the team builder) then read undefined.parentNode
 * and the server stopped drawing the page there. With this, none is null there too. Once.
 */
export function placeholderNullWhenNone(dropzone: object = DndDropzoneDirective.prototype): void {
  const proto = dropzone as Dropzone;
  if (proto.placeholderNullWhenNone)
    return;
  const tryGetPlaceholder = proto.tryGetPlaceholder;
  proto.tryGetPlaceholder = function (this: Dropzone) {
    return tryGetPlaceholder.call(this) ?? null;
  };
  proto.placeholderNullWhenNone = true;
}
