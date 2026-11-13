import { of } from 'rxjs';
import { noContentAsNull } from './noContentAsNull';

describe('noContentAsNull', () => {
  it('passes a body on, and gives null for a 204\'s missing one', () => {
    const seen: (string | null)[] = [];
    of('signup', null as unknown as string, undefined as unknown as string).pipe(noContentAsNull()).subscribe(v => seen.push(v));

    expect(seen).toEqual(['signup', null, null]);
  });
});
