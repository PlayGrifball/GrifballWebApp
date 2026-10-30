import { DEFAULT_ALLOWED_HOSTS, isAllowedHost, parseAllowedHosts } from './allowedHosts';

describe('allowed hosts', () => {
  it('adds NG_ALLOWED_HOSTS to the defaults, trimmed and in lower case', () => {
    expect(parseAllowedHosts(' Preview.Example.COM , *.Other.Example ,,')).toEqual([
      ...DEFAULT_ALLOWED_HOSTS, 'preview.example.com', '*.other.example',
    ]);
  });

  it('is just the defaults without NG_ALLOWED_HOSTS', () => {
    expect(parseAllowedHosts(undefined)).toEqual([...DEFAULT_ALLOWED_HOSTS]);
    expect(parseAllowedHosts('')).toEqual([...DEFAULT_ALLOWED_HOSTS]);
  });

  it('matches an entry given in upper case, whatever the Host header\'s case', () => {
    const allowed = parseAllowedHosts('Preview.Example.COM,*.Other.Example');
    expect(isAllowedHost('preview.example.com', allowed)).toBeTrue();
    expect(isAllowedHost('PREVIEW.example.com:8080', allowed)).toBeTrue();
    expect(isAllowedHost('a.other.example', allowed)).toBeTrue();
    expect(isAllowedHost('other.example', allowed)).toBeFalse();
  });

  it('allows the site, its subdomains and localhost, and nothing else', () => {
    const allowed = parseAllowedHosts(undefined);
    expect(isAllowedHost('grifball.xyz', allowed)).toBeTrue();
    expect(isAllowedHost('Grif-Preview1.Grifball.xyz', allowed)).toBeTrue();
    expect(isAllowedHost('localhost:4200', allowed)).toBeTrue();
    expect(isAllowedHost('evil.example', allowed)).toBeFalse();
    expect(isAllowedHost('10.0.0.5', allowed)).toBeFalse();
    expect(isAllowedHost('[::1]:80', allowed)).toBeFalse();
    expect(isAllowedHost(undefined, allowed)).toBeFalse();
    expect(isAllowedHost('', allowed)).toBeFalse();
  });
});
