import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { Employee } from '../core/models';
import { problemMessage } from '../core/problem';
import { elapsedSeconds, formatDuration } from '../core/time';

@Component({
  selector: 'tr[app-employee-row]',
  imports: [FormsModule],
  templateUrl: './employee-row.html',
})
export class EmployeeRow {
  private readonly api = inject(ApiService);

  readonly employee = input.required<Employee>();
  readonly now = input.required<number>();
  readonly changed = output<void>();

  protected readonly description = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly canStart = computed(() => this.description().trim().length > 0 && !this.busy());
  protected readonly elapsed = computed(() => {
    const active = this.employee().activeEntry;
    return active ? formatDuration(elapsedSeconds(active.startedAt, this.now())) : '';
  });

  protected start(): void {
    if (!this.canStart()) return;
    this.run(this.api.startTimer(this.employee().id, this.description().trim()), () =>
      this.description.set(''),
    );
  }

  protected stop(): void {
    this.run(this.api.stopTimer(this.employee().id));
  }

  private run(request: ReturnType<ApiService['stopTimer']>, onSuccess?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        onSuccess?.();
        this.changed.emit();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err));
        // The list may be stale (e.g. 409 because the timer state changed elsewhere).
        this.changed.emit();
      },
    });
  }
}
