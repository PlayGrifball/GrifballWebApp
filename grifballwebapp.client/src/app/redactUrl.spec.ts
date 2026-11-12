import { REDACTED, isSensitiveParam, redactUrl, redactUrlIn } from './redactUrl';

describe('redactUrl', () => {
  it('redacts the values of sign-in, reset and session parameters, whatever their case', () => {
    expect(redactUrl('/api/Identity/ConfirmEmail?userId=12&code=abc')).toBe(`/api/Identity/ConfirmEmail?userId=${REDACTED}&code=${REDACTED}`);
    expect(redactUrl('/reset?Email=a%40b.c&resetToken=xyz&access_token=t')).toBe(`/reset?Email=${REDACTED}&resetToken=${REDACTED}&access_token=${REDACTED}`);
    expect(redactUrl('/signin-discord?state=s&code=c')).toBe(`/signin-discord?state=${REDACTED}&code=${REDACTED}`);
    expect(redactUrl('/x?apiKey=k&password=p&sig=s&session=1')).toBe(`/x?apiKey=${REDACTED}&password=${REDACTED}&sig=${REDACTED}&session=${REDACTED}`);
  });

  it('keeps everything else, encoded', () => {
    expect(redactUrl('/api/Season/GetSeasons?ended=true&pageSize=10&q=a b')).toBe('/api/Season/GetSeasons?ended=true&pageSize=10&q=a%20b');
    expect(redactUrl('/season/12')).toBe('/season/12');
  });

  it('drops the scheme and host of an absolute URL', () => {
    expect(redactUrl('https://grifball.xyz/api/Teams?token=1')).toBe(`/api/Teams?token=${REDACTED}`);
  });

  it('gives nothing for no URL or one it cannot read', () => {
    expect(redactUrl(null)).toBe('');
    expect(redactUrl(undefined)).toBe('');
    expect(redactUrl('https://[bad')).toBe('');
  });

  it('tells sensitive names apart', () => {
    expect(isSensitiveParam('Token')).toBeTrue();
    expect(isSensitiveParam('pageNumber')).toBeFalse();
  });

  it('redacts a URL where a message quotes it', () => {
    const url = 'https://grifball.xyz/api/Identity/Reset?token=s3cret';
    expect(redactUrlIn(`Http failure response for ${url}: 500 Internal Server Error`, url))
      .toBe(`Http failure response for /api/Identity/Reset?token=${REDACTED}: 500 Internal Server Error`);
    expect(redactUrlIn('no url here', null)).toBe('no url here');
  });
});
