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
