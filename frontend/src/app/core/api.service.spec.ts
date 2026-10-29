import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from './api.service';

describe('ApiService', () => {
  let api: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets employees', () => {
    api.getEmployees().subscribe();
    const req = http.expectOne('/api/employees');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('creates an employee', () => {
    api.createEmployee('Alice').subscribe();
    const req = http.expectOne('/api/employees');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Alice' });
    req.flush({});
  });

  it('starts a timer', () => {
    api.startTimer(3, 'Docs').subscribe();
    const req = http.expectOne('/api/employees/3/timer/start');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ description: 'Docs' });
    req.flush({});
  });

  it('stops a timer', () => {
    api.stopTimer(3).subscribe();
    const req = http.expectOne('/api/employees/3/timer/stop');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('sends ISO report parameters', () => {
    const from = new Date(Date.UTC(2026, 0, 5, 0, 0, 0));
    const to = new Date(Date.UTC(2026, 0, 12, 0, 0, 0));
    api.getReport(from, to, 7).subscribe();
    const req = http.expectOne((r) => r.url === '/api/reports');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('from')).toBe('2026-01-05T00:00:00.000Z');
    expect(req.request.params.get('to')).toBe('2026-01-12T00:00:00.000Z');
    expect(req.request.params.get('employeeId')).toBe('7');
    req.flush({});
  });

  it('omits employeeId when not filtering', () => {
    api.getReport(new Date(0), new Date(1000)).subscribe();
    const req = http.expectOne((r) => r.url === '/api/reports');
    expect(req.request.params.has('employeeId')).toBe(false);
    req.flush({});
  });
});
