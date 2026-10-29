import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { Employee } from '../core/models';
import { problemMessage } from '../core/problem';
import { EmployeeRow } from './employee-row';

@Component({
  selector: 'app-employees-page',
  imports: [FormsModule, EmployeeRow],
  templateUrl: './employees.page.html',
  styleUrl: './employees.page.css',
})
export class EmployeesPage {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly employees = signal<Employee[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly newName = signal('');
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly now = signal(Date.now());

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
    this.load();
  }

  protected load(): void {
    this.api
      .getEmployees()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (employees) => {
          this.employees.set(employees);
          this.error.set(null);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(problemMessage(err));
          this.loading.set(false);
        },
      });
  }

  protected add(): void {
    const name = this.newName().trim();
    if (!name || this.saving()) return;

    this.saving.set(true);
    this.formError.set(null);
    this.api
      .createEmployee(name)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (employee) => {
          this.employees.update((list) => [...list, employee]);
          this.newName.set('');
          this.saving.set(false);
        },
        error: (err) => {
          this.formError.set(problemMessage(err));
          this.saving.set(false);
        },
      });
  }
}
