import { signal } from '@angular/core';
import { StoreInput } from './storeInput';

describe('StoreInput', () => {
  it('is undefined until an input is handed over, then follows it', () => {
    const userID = new StoreInput<number>();
    expect(userID.value()).toBeUndefined();

    const input = signal(117);
    userID.use(input);
    expect(userID.value()).toBe(117);

    input.set(999);
    expect(userID.value()).toBe(999);
  });

  it('follows the input handed over last', () => {
    const page = new StoreInput<number>();
    page.use(signal(1));
    page.use(signal(2));
    expect(page.value()).toBe(2);
  });
});
