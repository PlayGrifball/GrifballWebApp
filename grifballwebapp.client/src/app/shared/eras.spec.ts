import { activeYearSpan, grifballEra } from './eras';

describe('eras', () => {
  it('maps each historical period and Halo Infinite to its Grifball era', () => {
    expect(grifballEra(2008, 'halo3').id).toBe('founding');
    expect(grifballEra(2012, 'reach').id).toBe('expansion');
    expect(grifballEra(2016, 'halo5').id).toBe('hub');
    expect(grifballEra(2021, 'halo3').id).toBe('revival');
    expect(grifballEra(2024, 'infinite').id).toBe('infinite');
    expect(grifballEra(null, 'infinite').id).toBe('infinite');
    expect(grifballEra(null, null).id).toBe('revival');
    expect(grifballEra(0, null).id).toBe('revival');
  });

  it('gives each era a span that starts where the last one ends', () => {
    const at = (year: number) => grifballEra(year, 'halo3').span;
    expect([2010, 2011, 2013, 2014, 2018, 2019, 2022].map(at)).toEqual([
      '2008–2010', '2011–2013', '2011–2013', '2014–2018', '2014–2018', '2019–2022', '2019–2022']);
  });

  it('formats an active year span from season years', () => {
    expect(activeYearSpan([])).toBeNull();
    expect(activeYearSpan([null, 0])).toBeNull();
    expect(activeYearSpan([2025])).toBe('2025');
    expect(activeYearSpan([2024, 2009, 2026])).toBe('2009 – 2026');
  });
});
