import { HttpErrorResponse } from '@angular/common/http';
import { problemMessage } from './problem';

const failure = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

describe('problemMessage', () => {
  it('joins validation errors', () => {
    const err = failure(422, {
      title: 'Validation failed',
      errors: { name: ['Name is required.'], other: ['Too long.'] },
    });
    expect(problemMessage(err)).toBe('Name is required. Too long.');
  });

  it('uses the detail of a 409', () => {
    const err = failure(409, { title: 'Conflict', detail: 'Timer is already running for this employee.' });
    expect(problemMessage(err)).toBe('Timer is already running for this employee.');
  });

  it('falls back to the title', () => {
    expect(problemMessage(failure(400, { title: 'Bad Request' }))).toBe('Bad Request');
  });

  it('reports an unreachable server', () => {
    expect(problemMessage(failure(0, new ProgressEvent('error')))).toBe('Server unreachable');
  });

  it('falls back for anything else', () => {
    expect(problemMessage(new Error('boom'))).toBe('Unexpected error');
    expect(problemMessage(failure(500, 'plain text'))).toBe('Unexpected error');
  });
});
