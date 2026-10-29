import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { EmployeesPage } from './employees.page';

describe('EmployeesPage', () => {
  afterEach(() => vi.restoreAllMocks());

  it('shows the empty state', async () => {
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: { getEmployees: () => of([]) } }] });
    const fixture = TestBed.createComponent(EmployeesPage);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('No employees yet. Add the first one above.');
  });

  it('stops its clock interval when destroyed', () => {
    const setSpy = vi.spyOn(globalThis, 'setInterval');
    const clearSpy = vi.spyOn(globalThis, 'clearInterval');
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: { getEmployees: () => of([]) } }] });
    const fixture = TestBed.createComponent(EmployeesPage);
    const clockCall = setSpy.mock.calls.findIndex(([, ms]) => ms === 1000);
    expect(clockCall).toBeGreaterThanOrEqual(0);
    const clockId = setSpy.mock.results[clockCall].value;

    fixture.destroy();

    expect(clearSpy).toHaveBeenCalledWith(clockId);
  });

  it('reloads the list in server order after adding an employee', async () => {
    const aaron = { id: 2, name: 'Aaron', createdAt: '2026-01-05T08:00:00Z', activeEntry: null };
    const alice = { id: 1, name: 'Alice', createdAt: '2026-01-05T07:00:00Z', activeEntry: null };
    const getEmployees = vi.fn().mockReturnValueOnce(of([alice])).mockReturnValue(of([aaron, alice]));
    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: { getEmployees, createEmployee: () => of(aaron) } }],
    });
    const fixture = TestBed.createComponent(EmployeesPage);
    await fixture.whenStable();

    fixture.componentInstance['newName'].set('Aaron');
    fixture.componentInstance['add']();
    await fixture.whenStable();

    const names = [...fixture.nativeElement.querySelectorAll('tbody tr')].map((r: HTMLElement) => r.textContent);
    expect(names[0]).toContain('Aaron');
    expect(names[1]).toContain('Alice');
  });

  it('ignores a stale list response that arrives after a newer one', async () => {
    const first = new Subject<never[]>();
    const second = new Subject<{ id: number; name: string; createdAt: string; activeEntry: null }[]>();
    const getEmployees = vi.fn().mockReturnValueOnce(first).mockReturnValueOnce(second);
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: { getEmployees } }] });
    const fixture = TestBed.createComponent(EmployeesPage);
    fixture.componentInstance['load']();

    second.next([{ id: 1, name: 'Alice', createdAt: '2026-01-05T07:00:00Z', activeEntry: null }]);
    first.next([]);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Alice');
  });
});
