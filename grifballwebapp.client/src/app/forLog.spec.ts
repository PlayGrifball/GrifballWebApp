import { forLog } from './forLog';

describe('forLog', () => {
  it('keeps a URL without line breaks as it is', () => {
    expect(forLog('/season/12?tab=schedule')).toBe('/season/12?tab=schedule');
  });

  it('turns line breaks into spaces, so no line of its own can start', () => {
    expect(forLog('/a\r\nERROR forged\nentry\r')).toBe('/a  ERROR forged entry ');
  });
});
