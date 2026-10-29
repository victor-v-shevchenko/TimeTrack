import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { Employee, Report } from '../core/models';
import { problemMessage } from '../core/problem';
import { currentWeek, formatDuration, toRange } from '../core/time';

@Component({
  selector: 'app-report-page',
  imports: [FormsModule, DatePipe],
  templateUrl: './report.page.html',
  styleUrl: './report.page.css',
})
export class ReportPage {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatDuration = formatDuration;

  protected readonly from = signal(currentWeek().from);
  protected readonly to = signal(currentWeek().to);
  protected readonly employeeId = signal<number | null>(null);
  protected readonly employees = signal<Employee[]>([]);
  protected readonly report = signal<Report | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.api
      .getEmployees()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (employees) => this.employees.set(employees),
        error: (err) => this.error.set(problemMessage(err)),
      });
    this.show();
  }

  protected show(): void {
    if (!this.from() || !this.to()) {
      this.error.set('Select both dates.');
      return;
    }
    if (this.to() < this.from()) {
      this.error.set("'To' date must not be before 'From' date.");
      return;
    }

    const { from, to } = toRange(this.from(), this.to());
    this.loading.set(true);
    this.error.set(null);
    this.api
      .getReport(from, to, this.employeeId() ?? undefined)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (report) => {
          this.report.set(report);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(problemMessage(err));
          this.loading.set(false);
        },
      });
  }
}
