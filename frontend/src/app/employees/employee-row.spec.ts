import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Employee } from '../core/models';
import { EmployeeRow } from './employee-row';

@Component({
  imports: [EmployeeRow],
  template: `<table>
    <tbody>
      <tr app-employee-row [employee]="employee()" [now]="now()" (changed)="changes = changes + 1"></tr>
    </tbody>
  </table>`,
})
class Host {
  employee = signal<Employee>({ id: 1, name: 'Alice Example', createdAt: '2026-01-05T08:00:00Z', activeEntry: null });
  now = signal(Date.parse('2026-01-05T09:00:00Z'));
  changes = 0;
}

describe('EmployeeRow', () => {
  const api = {
    startTimer: vi.fn(() => of({})),
    stopTimer: vi.fn(() => of({})),
  };

  beforeEach(() => {
    api.startTimer.mockClear();
    api.stopTimer.mockClear();
    TestBed.configureTestingModule({ providers: [{ provide: ApiService, useValue: api }] });
  });

  it('starts a timer when not running', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;

    const start = [...el.querySelectorAll('button')].find((b) => b.textContent?.includes('Start'))!;
    expect(start.disabled).toBe(true);
    expect(el.textContent).not.toContain('Stop');

    const input = el.querySelector('input')!;
    input.value = '  Write docs ';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(start.disabled).toBe(false);

    start.click();
    await fixture.whenStable();

    expect(api.startTimer).toHaveBeenCalledWith(1, 'Write docs');
    expect(fixture.componentInstance.changes).toBe(1);
  });

  it('shows elapsed time and stops a running timer', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.employee.update((e) => ({
      ...e,
      activeEntry: { id: 5, description: 'Write docs', startedAt: '2026-01-05T07:58:58Z' },
    }));
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Write docs');
    expect(el.textContent).toContain('1:01:02');
    expect(el.textContent).not.toContain('Start');

    fixture.componentInstance.now.set(Date.parse('2026-01-05T09:00:03Z'));
    await fixture.whenStable();
    expect(el.textContent).toContain('1:01:05');

    [...el.querySelectorAll('button')].find((b) => b.textContent?.includes('Stop'))!.click();
    await fixture.whenStable();

    expect(api.stopTimer).toHaveBeenCalledWith(1);
    expect(fixture.componentInstance.changes).toBe(1);
  });
});
