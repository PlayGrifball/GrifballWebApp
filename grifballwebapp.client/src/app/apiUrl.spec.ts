import { apiUrl, DEFAULT_API_URL, withoutTrailingSlashes } from './apiUrl';

describe('apiUrl', () => {
  it('is the backend in the cluster when API_URL is not set', () => {
    expect(apiUrl(undefined)).toBe(DEFAULT_API_URL);
  });

  it('is API_URL, without its trailing slashes', () => {
    expect(apiUrl('https://api.example:8443')).toBe('https://api.example:8443');
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address form (plain HTTP), which apiUrl takes
    expect(apiUrl('http://backend:5295///')).toBe('http://backend:5295');
  });

  it('takes off only the slashes a URL ends with', () => {
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address form (plain HTTP), which apiUrl takes
    expect(withoutTrailingSlashes('http://a/b/')).toBe('http://a/b');
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address form (plain HTTP), which apiUrl takes
    expect(withoutTrailingSlashes('http://a//b')).toBe('http://a//b');
    expect(withoutTrailingSlashes('///')).toBe('');
    expect(withoutTrailingSlashes('')).toBe('');
  });
});
