import { seasonFormatLabel, seasonFormats } from './seasonFormat';

describe('seasonFormat', () => {
  it('labels each format, a missing one as a league season', () => {
    expect(seasonFormats.map(f => seasonFormatLabel(f.value))).toEqual(['League season', 'Tournament']);
    expect(seasonFormatLabel(null)).toBe('League season');
    expect(seasonFormatLabel(undefined)).toBe('League season');
  });
});
