import { HttpErrorResponse } from '@angular/common/http';

export function problemMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) {
    return 'Unexpected error';
  }
  if (err.status === 0) {
    return 'Server unreachable';
  }
  const body = err.error;
  if (body && typeof body === 'object') {
    if (body.errors && typeof body.errors === 'object') {
      const messages = Object.values<unknown>(body.errors).flat().map(String);
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }
    return body.detail ?? body.title ?? 'Unexpected error';
  }
  return 'Unexpected error';
}
