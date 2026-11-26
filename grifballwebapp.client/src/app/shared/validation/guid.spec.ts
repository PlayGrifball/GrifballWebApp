import { GUID_PATTERN } from './guid';

describe('GUID_PATTERN', () => {
  const matches = (value: string): boolean => new RegExp(GUID_PATTERN, 'g').test(value);

  it('matches a bare GUID', () => {
    expect(matches('0f8fad5b-d9cb-469f-a165-70867728950e')).toBeTrue();
  });

  it('matches a GUID wrapped in a balanced pair of braces', () => {
    expect(matches('{0f8fad5b-d9cb-469f-a165-70867728950e}')).toBeTrue();
  });

  it('rejects an opening brace with no closing one', () => {
    expect(matches('{0f8fad5b-d9cb-469f-a165-70867728950e')).toBeFalse();
  });

  it('rejects a closing brace with no opening one', () => {
    expect(matches('0f8fad5b-d9cb-469f-a165-70867728950e}')).toBeFalse();
  });

  it('rejects anything that is not a GUID', () => {
    expect(matches('nope')).toBeFalse();
  });
});
