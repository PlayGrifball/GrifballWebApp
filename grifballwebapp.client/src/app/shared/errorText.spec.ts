import { HttpErrorResponse } from '@angular/common/http';
import { errorText, problemText } from './errorText';

describe('errorText', () => {
  const response = (error: unknown) => new HttpErrorResponse({ error, status: 400 });

  it('reads a ProblemDetails detail, else its title', () => {
    expect(errorText(response({ title: 'Request refused', detail: 'Signups are closed' }), 'x')).toBe('Signups are closed');
    expect(errorText(response({ title: 'Conflict', detail: null }), 'x')).toBe('Conflict');
  });

  it('reads a plain-text message', () => {
    expect(errorText(response('Plain reason'), 'x')).toBe('Plain reason');
  });

  it('falls back with no reason', () => {
    expect(errorText(response({}), 'fallback')).toBe('fallback');
    expect(errorText(response(''), 'fallback')).toBe('fallback');
    expect(errorText(response(null), 'fallback')).toBe('fallback');
  });

  it('problemText reads the same', () => {
    expect(problemText(response({ detail: 'Why' }), 'x')).toBe('Why');
  });
});

describe('errorText of a request that read text', () => {
  const response = (error: unknown) => new HttpErrorResponse({ error, status: 400 });

  it("reads a ProblemDetails sent as JSON text", () => {
    expect(errorText(response('{"title":"Request refused","detail":"Token is required"}'), 'x')).toBe('Token is required');
    expect(errorText(response('{"title":"Conflict"}'), 'x')).toBe('Conflict');
  });

  it('falls back for JSON text that is no ProblemDetails, or not JSON', () => {
    expect(errorText(response('{"other":1}'), 'fallback')).toBe('fallback');
    expect(errorText(response('{not json'), 'fallback')).toBe('fallback');
  });
});
