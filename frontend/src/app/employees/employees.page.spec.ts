import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
});
