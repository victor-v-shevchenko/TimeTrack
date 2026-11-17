import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Report } from '../core/models';
import { ReportPage } from './report.page';

describe('ReportPage', () => {
  it('shows the empty state and zero totals', async () => {
    const api = {
      getEmployees: () =>
        of([{ id: 1, name: 'Alice Example', createdAt: '2026-01-05T00:00:00Z', activeEntry: null }]),
      getReport: () =>
        of({
          from: '2026-01-05T00:00:00Z',
          to: '2026-01-12T00:00:00Z',
          entries: [],
          totals: [{ employeeId: 1, employeeName: 'Alice Example', totalSeconds: 0 }],
        }),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });

    const fixture = TestBed.createComponent(ReportPage);
    await fixture.whenStable();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('No records for this period');
    expect(text).toContain('Alice Example');
    expect(text).toContain('0:00:00');
    expect(fixture.nativeElement.querySelectorAll('table').length).toBe(1);
  });

  it('keeps the latest report when an older response arrives after it', async () => {
    const responses: Subject<Report>[] = [];
    const getReport = vi.fn(() => {
      const response = new Subject<Report>();
      responses.push(response);
      return response;
    });
    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: { getEmployees: () => of([]), getReport } }],
    });
    const fixture = TestBed.createComponent(ReportPage);
    await fixture.whenStable();
    const page = fixture.componentInstance as unknown as { show(): void; report(): Report | null };
    const report = (from: string): Report => ({
      from,
      to: '2026-01-12T00:00:00Z',
      entries: [],
      totals: [],
    });

    page.show();
    page.show();
    const [initial, older, newer] = responses;
    newer.next(report('newer'));
    older.next(report('older'));
    initial.next(report('initial'));

    expect(page.report()?.from).toBe('newer');
    expect(older.observed).toBe(false);
  });

  it('does not call the API when the end date is before the start date', async () => {
    const getReport = vi.fn(() => of({ from: '', to: '', entries: [], totals: [] }));
    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: { getEmployees: () => of([]), getReport } }],
    });
    const fixture = TestBed.createComponent(ReportPage);
    await fixture.whenStable();
    getReport.mockClear();

    const page = fixture.componentInstance as unknown as {
      from: { set(v: string): void };
      to: { set(v: string): void };
      show(): void;
    };
    page.from.set('2026-02-10');
    page.to.set('2026-02-01');
    page.show();
    await fixture.whenStable();

    expect(getReport).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain("'To' date must not be before");
  });
});
